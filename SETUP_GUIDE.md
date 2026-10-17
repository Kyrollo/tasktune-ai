# TaskTune AI - Complete Project Setup Guide

Welcome to the TaskTune AI Setup Guide. This repository coordinates the 3 main parts of our application: the React Frontend, the .NET Core Backend, and the Python AI Agents.

## 1. Database Setup & Population

The application requires a Microsoft SQL Server database.

1. Open **SQL Server Management Studio (SSMS)** or Azure Data Studio.
2. Connect to your local SQL Server instance (e.g., `(localdb)\MSSQLLocalDB` or `.\SQLEXPRESS`).
3. Open the main database script located at:
   `tasktune-backend\Script Database\setup_database.sql`
4. Execute the script. This will drop any existing `TaskTuneAIDatabase`, create a fresh one, build the entire schema, and seed it with initial data (demo users, companies, projects, departments, and tasks).

### Optional: Massive Mock Data
If you want to fully populate the app with realistic historical and large-scale data for rich reporting, run these supplementary scripts in order:
1. `tasktune-backend\Script Database\insert_tasktune_mock_data.sql`
2. `tasktune-backend\Script Database\seed_massive_tasktune_data.sql`

*Note: The demo accounts generated all share the password:* `Password123!`
*Demo Employee:* `employee@tasktune.ai`
*Demo Supervisor:* `supervisor@tasktune.ai`
*Demo Admin:* `ceo@tasktune.ai`

---

## 2. Backend API (.NET 8)

1. Open the file: `tasktune-backend\TaskTuneAPI\TaskTune.API\appsettings.json`
2. Update the `"DefaultConnection"` string to match your SQL Server instance name.
   *(Example: `Server=YOUR_SERVER_NAME;Database=TaskTuneAIDatabase;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`)*
           // "DefaultConnection": "Data Source=localhost;Initial Catalog=TaskTuneAIDatabase;Integrated Security=True;TrustServerCertificate=True;"
3. Open a terminal and navigate to the API directory:
   ```bash
   cd tasktune-backend/TaskTuneAPI/TaskTune.API
   ```
4. Build and run the API:
   ```bash
   dotnet restore
   dotnet run
   ```
*The backend should now run on* `http://localhost:5000`

---

## 3. AI Agents (Python FastAPI)

1. Open a new terminal and navigate to the agents directory:
   ```bash
   cd tasktune-agents
   ```
2. Create your environment file by duplicating the provided template:
   ```bash
   cp .env.example .env
   ```
3. Open the newly created `.env` file and insert your free API key from [Groq Console](https://console.groq.com/keys):
   `GROQ_API_KEY=your-api-key-here`
4. Install the required Python packages (requires Python 3.10+):
   ```bash
   pip install -r requirements.txt
   ```
5. Launch all three AI agents simultaneously using the orchestration script:
   ```bash
   python start_all_agents.py
   ```
*The Guidance, Tracking, and Reporter agents will spawn on ports `5050`, `8003`, and `8004` respectively.*

---

## 4. Web Frontend (React + Vite)

1. Open a new terminal and navigate to the frontend directory:
   ```bash
   cd tasktune-frontend
   ```
2. Install the Node packages:
   ```bash
   npm install
   ```
3. Start the development server:
   ```bash
   npm run dev
   ```
*The website will now be running at* `http://localhost:5173`

---

## Conclusion
You now have a fully operational 5-service architecture running smoothly! Open your browser to `http://localhost:5173` and log in with one of the seeded demo accounts (e.g. `employee@tasktune.ai` / `Password123!`) to test out the application and test out the AI Task Guidance and Report Generation features.
