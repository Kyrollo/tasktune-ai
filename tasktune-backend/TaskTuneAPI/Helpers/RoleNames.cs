namespace TaskTuneAPI.Helpers
{
    public static class RoleNames
    {
        public const byte Employee = 0;
        public const byte Supervisor = 1;
        public const byte Manager = 2;
        public const byte CEO = 3;
        public const byte HR = 4;
        public const byte Admin = 5;

        public static string GetName(byte role)
        {
            switch (role)
            {
                case 0: return "Employee";
                case 1: return "Supervisor";
                case 2: return "Manager";
                case 3: return "CEO";
                case 4: return "HR";
                case 5: return "Admin";
                default: return "Unknown";
            }
        }
    }
}