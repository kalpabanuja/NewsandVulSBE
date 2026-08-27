# VPS Migration Instructions (Database Wipe)

Because we have completely changed the structure of the database (by splitting `Vulnerabilities` into `PendingVulnerabilities` and `ReleasedVulnerabilities`), the simplest and safest way to apply this change to your existing VPS without dealing with complicated EF Core rollback errors is to simply wipe the old database volume and start fresh.

Because we are removing the old data, you will start with an empty database until you run the historical data seeder script.

### Step-by-Step Instructions

1. SSH into your VPS and navigate to the project directory:
   ```bash
   cd NewsandVulSBE
   ```
2. **Pull the latest code changes** from Git:
   ```bash
   git pull origin main
   ```
3. **Bring down the existing containers AND wipe the database volume:**
   ```bash
   sudo docker compose down -v
   ```
   *(The `-v` flag is important here. It tells Docker to delete the old PostgreSQL volume where the old schema is saved).*
4. **Rebuild and bring up the new containers:**
   ```bash
   sudo docker compose up -d --build
   ```
5. **Wait for initialization:**
   The backend will start and create the new database tables. You can verify it is running by checking the UI dashboard. Then, run the historical data seeder script (see `UpdatewithOldData/ScryptGuide.md`) to populate old data.
