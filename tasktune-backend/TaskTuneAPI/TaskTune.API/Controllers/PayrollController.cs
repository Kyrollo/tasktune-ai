using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/payroll")]
[RequireModule("core_hr")]
[Authorize]
public sealed class PayrollController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    public PayrollController(TaskTuneDbContext db) => _db = db;

    [HttpGet("periods")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetPeriods()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var items = await _db.PayrollPeriods.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value)
            .OrderByDescending(x => x.PeriodStart)
            .Select(x => new { x.PayrollPeriodId, x.PeriodStart, x.PeriodEnd, x.CurrencyCode, x.Status, x.CreatedAt, x.ApprovedAt })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("periods")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreatePeriod([FromBody] CreatePayrollPeriodRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var currency = (request.CurrencyCode ?? "").Trim().ToUpperInvariant();
        if (request.PeriodEnd < request.PeriodStart || !PayrollCalculationPolicy.IsValidCurrency(currency))
            return BadRequest(new { message = "Payroll dates must be ordered and currency must be a three-letter code." });
        var exists = await _db.PayrollPeriods.AnyAsync(x => x.CompanyId == companyId.Value && x.PeriodStart == request.PeriodStart && x.PeriodEnd == request.PeriodEnd);
        if (exists) return Conflict(new { message = "A payroll period with these dates already exists." });
        var period = new PayrollPeriod { CompanyId = companyId.Value, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, CurrencyCode = currency, Status = "draft", CreatedBy = actorId.Value, CreatedAt = DateTime.UtcNow };
        _db.PayrollPeriods.Add(period); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "PayrollPeriod", period.PayrollPeriodId, after: new { period.PeriodStart, period.PeriodEnd, period.CurrencyCode });
        return CreatedAtAction(nameof(GetPeriods), new { }, new { period.PayrollPeriodId, period.PeriodStart, period.PeriodEnd, period.CurrencyCode, period.Status });
    }

    [HttpGet("periods/{periodId:int}/payslips")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetPeriodPayslips(int periodId)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var periodExists = await _db.PayrollPeriods.AnyAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == periodId);
        if (!periodExists) return NotFound();
        var items = await (
            from payslip in _db.Payslips.AsNoTracking()
            join employee in _db.Users.AsNoTracking() on payslip.UserId equals employee.UserId
            where payslip.CompanyId == companyId.Value && payslip.PayrollPeriodId == periodId && employee.CompanyId == companyId.Value
            orderby employee.LastName, employee.FirstName
            select new
            {
                payslip.PayslipId, payslip.UserId,
                EmployeeName = employee.FullName ?? (employee.FirstName + " " + employee.LastName),
                payslip.BaseSalary, payslip.Allowances, payslip.Bonus,
                GrossPay = payslip.BaseSalary + payslip.Allowances + payslip.Bonus,
                payslip.IncomeTaxDeduction, payslip.SocialInsuranceDeduction, payslip.OtherDeductions,
                NetPay = payslip.BaseSalary + payslip.Allowances + payslip.Bonus - payslip.IncomeTaxDeduction - payslip.SocialInsuranceDeduction - payslip.OtherDeductions,
                payslip.CurrencyCode, payslip.Status, payslip.Notes, payslip.PaidAt
            }).ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("periods/{periodId:int}/payslips")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> AddPayslip(int periodId, [FromBody] CreatePayslipRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var period = await _db.PayrollPeriods.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == periodId);
        if (period == null) return NotFound();
        if (period.Status != "draft") return Conflict(new { message = "Payslips can only be changed while the payroll period is a draft." });
        var requestedCurrency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? period.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(requestedCurrency, period.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "All payslips in a payroll period must use the period currency." });
        var currency = period.CurrencyCode;
        if (!PayrollCalculationPolicy.IsValidCurrency(currency) ||
            !PayrollCalculationPolicy.IsValidPayComponents(request.BaseSalary, request.Allowances, request.Bonus, request.IncomeTaxDeduction, request.SocialInsuranceDeduction, request.OtherDeductions))
            return BadRequest(new { message = "Pay components must be non-negative, deductions cannot exceed gross pay, and currency must be a three-letter code." });
        var employeeExists = await _db.Users.AnyAsync(x => x.CompanyId == companyId.Value && x.UserId == request.UserId && x.IsActive);
        if (!employeeExists) return NotFound(new { message = "Active employee was not found in this company." });
        if (await _db.Payslips.AnyAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == periodId && x.UserId == request.UserId))
            return Conflict(new { message = "A payslip already exists for this employee in this period." });
        var slip = new Payslip { CompanyId = companyId.Value, PayrollPeriodId = periodId, UserId = request.UserId, BaseSalary = request.BaseSalary, Allowances = request.Allowances, Bonus = request.Bonus, IncomeTaxDeduction = request.IncomeTaxDeduction, SocialInsuranceDeduction = request.SocialInsuranceDeduction, OtherDeductions = request.OtherDeductions, CurrencyCode = currency, Status = "draft", Notes = request.Notes?.Trim(), CreatedAt = DateTime.UtcNow };
        _db.Payslips.Add(slip); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "Payslip", slip.PayslipId, after: new { slip.UserId, slip.BaseSalary, slip.CurrencyCode });
        return StatusCode(201, new { slip.PayslipId, slip.UserId, grossPay = PayrollCalculationPolicy.Gross(slip.BaseSalary, slip.Allowances, slip.Bonus), netPay = PayrollCalculationPolicy.Net(slip.BaseSalary, slip.Allowances, slip.Bonus, slip.IncomeTaxDeduction, slip.SocialInsuranceDeduction, slip.OtherDeductions), slip.Status });
    }

    [HttpPost("periods/{periodId:int}/approve")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> ApprovePeriod(int periodId)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var period = await _db.PayrollPeriods.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == periodId);
        if (period == null) return NotFound();
        if (period.Status != "draft") return Conflict(new { message = "Only a draft payroll period can be approved." });
        var slips = await _db.Payslips.Where(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == periodId).ToListAsync();
        if (slips.Count == 0) return Conflict(new { message = "Add at least one payslip before approving payroll." });
        period.Status = "approved"; period.ApprovedAt = DateTime.UtcNow; period.ApprovedBy = actorId.Value;
        foreach (var slip in slips) slip.Status = "approved";
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "APPROVE", "PayrollPeriod", periodId, after: new { period.Status, payslipCount = slips.Count });
        return Ok(new { period.PayrollPeriodId, period.Status, payslipCount = slips.Count, period.ApprovedAt });
    }

    [HttpPost("payslips/{payslipId:int}/mark-paid")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> MarkPaid(int payslipId)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var slip = await _db.Payslips.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.PayslipId == payslipId);
        if (slip == null) return NotFound();
        if (slip.Status != "approved") return Conflict(new { message = "Only an approved payslip can be marked paid." });
        slip.Status = "paid"; slip.PaidAt = DateTime.UtcNow; slip.PaidBy = actorId.Value;
        var remaining = await _db.Payslips.AnyAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == slip.PayrollPeriodId && x.Status != "paid" && x.PayslipId != slip.PayslipId);
        if (!remaining)
        {
            var period = await _db.PayrollPeriods.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.PayrollPeriodId == slip.PayrollPeriodId);
            if (period != null) period.Status = "paid";
        }
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "PAID", "Payslip", payslipId, after: new { slip.Status, slip.PaidAt });
        return Ok(new { slip.PayslipId, slip.Status, slip.PaidAt });
    }

    [HttpGet("me/payslips")]
    public async Task<IActionResult> GetMyPayslips()
    {
        var companyId = GetCurrentCompanyId(); var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();
        var items = await (
            from slip in _db.Payslips.AsNoTracking()
            join period in _db.PayrollPeriods.AsNoTracking() on slip.PayrollPeriodId equals period.PayrollPeriodId
            where slip.CompanyId == companyId.Value && slip.UserId == userId.Value && period.CompanyId == companyId.Value
            orderby period.PeriodStart descending
            select new
            {
                slip.PayslipId, period.PeriodStart, period.PeriodEnd,
                slip.BaseSalary, slip.Allowances, slip.Bonus,
                GrossPay = slip.BaseSalary + slip.Allowances + slip.Bonus,
                slip.IncomeTaxDeduction, slip.SocialInsuranceDeduction, slip.OtherDeductions,
                NetPay = slip.BaseSalary + slip.Allowances + slip.Bonus - slip.IncomeTaxDeduction - slip.SocialInsuranceDeduction - slip.OtherDeductions,
                slip.CurrencyCode, slip.Status, slip.PaidAt, slip.Notes
            }).ToListAsync();
        return Ok(new { items });
    }
}

public sealed record CreatePayrollPeriodRequest(DateOnly PeriodStart, DateOnly PeriodEnd, string CurrencyCode);
public sealed record CreatePayslipRequest(int UserId, decimal BaseSalary, decimal Allowances, decimal Bonus, decimal IncomeTaxDeduction, decimal SocialInsuranceDeduction, decimal OtherDeductions, string? CurrencyCode, string? Notes);
