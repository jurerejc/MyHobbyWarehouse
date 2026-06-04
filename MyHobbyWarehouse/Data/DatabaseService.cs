using Microsoft.Data.Sqlite;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Data;

public class DatabaseService
{
    private readonly string _cs;
    public static DatabaseService? Current { get; private set; }

    public DatabaseService(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _cs = $"Data Source={dbPath}";
        Current = this;
        InitializeDatabase();
    }

    // ── Connection ───────────────────────────────────────────────────────────

    private SqliteConnection Connect()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return c;
    }

    // ── Schema ───────────────────────────────────────────────────────────────

    private void InitializeDatabase()
    {
        using var c = Connect();

        Exec(c, @"
            CREATE TABLE IF NOT EXISTS Components (
                Sku              TEXT PRIMARY KEY,
                OldSku           TEXT NOT NULL DEFAULT '',
                Alt              TEXT NOT NULL DEFAULT '',
                Description      TEXT NOT NULL DEFAULT '',
                Unit             TEXT NOT NULL DEFAULT 'pcs',
                StockSum         REAL NOT NULL DEFAULT 0,
                Stock            REAL NOT NULL DEFAULT 0,
                StockRfu1        REAL NOT NULL DEFAULT 0,
                StockRfu2        REAL NOT NULL DEFAULT 0,
                StockRfu3        REAL NOT NULL DEFAULT 0,
                StockRfu4        REAL NOT NULL DEFAULT 0,
                StockRack        INTEGER NOT NULL DEFAULT 0,
                StockPackage     INTEGER NOT NULL DEFAULT 0,
                StockZone        INTEGER NOT NULL DEFAULT 0,
                LastPrice        REAL NOT NULL DEFAULT 0,
                StockValue       REAL NOT NULL DEFAULT 0,
                SupCount         INTEGER NOT NULL DEFAULT 0,
                LastSupplier     TEXT NOT NULL DEFAULT '',
                LastSupplierSku  TEXT NOT NULL DEFAULT '',
                MassMg           REAL NOT NULL DEFAULT 0,
                Smd              INTEGER NOT NULL DEFAULT 0,
                Category1        TEXT NOT NULL DEFAULT '',
                Category2        TEXT NOT NULL DEFAULT '',
                Category3        TEXT NOT NULL DEFAULT '',
                Category4        TEXT NOT NULL DEFAULT '',
                Category5        TEXT NOT NULL DEFAULT '',
                ManufacturerName TEXT NOT NULL DEFAULT '',
                ManufacturerPart TEXT NOT NULL DEFAULT '',
                Supplier1Name    TEXT NOT NULL DEFAULT '',
                Supplier1Sku     TEXT NOT NULL DEFAULT '',
                Supplier1Price   REAL NOT NULL DEFAULT 0,
                Supplier2Name    TEXT NOT NULL DEFAULT '',
                Supplier2Sku     TEXT NOT NULL DEFAULT '',
                Supplier2Price   REAL NOT NULL DEFAULT 0,
                Supplier3Name    TEXT NOT NULL DEFAULT '',
                Supplier3Sku     TEXT NOT NULL DEFAULT '',
                Supplier3Price   REAL NOT NULL DEFAULT 0,
                Supplier1Url     TEXT NOT NULL DEFAULT '',
                Supplier2Url     TEXT NOT NULL DEFAULT '',
                Supplier3Url     TEXT NOT NULL DEFAULT '',
                StickerText      TEXT NOT NULL DEFAULT ''
            );");

        Exec(c, @"
            CREATE TABLE IF NOT EXISTS Projects (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Name         TEXT NOT NULL DEFAULT '',
                BoardName    TEXT NOT NULL DEFAULT '',
                Version      TEXT NOT NULL DEFAULT '',
                Revision     TEXT NOT NULL DEFAULT '',
                Description  TEXT NOT NULL DEFAULT '',
                Notes        TEXT NOT NULL DEFAULT '',
                CreatedDate  TEXT NOT NULL,
                ModifiedDate TEXT NOT NULL
            );");

        Exec(c, @"
            CREATE TABLE IF NOT EXISTS BomLines (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectId       INTEGER NOT NULL,
                Sku             TEXT NOT NULL DEFAULT '',
                Sku2            TEXT NOT NULL DEFAULT '',
                Qty             REAL NOT NULL DEFAULT 0,
                Unit            TEXT NOT NULL DEFAULT 'pcs',
                PartDesignators TEXT NOT NULL DEFAULT '',
                Value           TEXT NOT NULL DEFAULT '',
                Device          TEXT NOT NULL DEFAULT '',
                Package         TEXT NOT NULL DEFAULT '',
                Notes           TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
            );");

        // Migration: add new columns if upgrading
        try { Exec(c, "ALTER TABLE Projects ADD COLUMN Version TEXT NOT NULL DEFAULT ''"); }
        catch { /* already exists */ }

        // Migration: add URL columns if upgrading from older schema
        foreach (var col in new[] { "Supplier1Url", "Supplier2Url", "Supplier3Url" })
        {
            try { Exec(c, $"ALTER TABLE Components ADD COLUMN {col} TEXT NOT NULL DEFAULT ''"); }
            catch { /* column already exists */ }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Locations table & migration
        // ════════════════════════════════════════════════════════════════════
        Exec(c, @"
            CREATE TABLE IF NOT EXISTS Locations (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Code        TEXT NOT NULL UNIQUE,
                Description TEXT NOT NULL DEFAULT ''
            );");

        try { Exec(c, "ALTER TABLE Components ADD COLUMN LocationId INTEGER NULL REFERENCES Locations(Id)"); }
        catch { /* column already exists */ }

        // One-time migration: create Location records from existing StockRack/StockPackage
        long existingLocCount = Scalar<long>(c, "SELECT COUNT(*) FROM Locations");
        if (existingLocCount == 0)
        {
            using var migCmd = c.CreateCommand();
            migCmd.CommandText = @"
                SELECT DISTINCT StockRack, StockPackage FROM Components
                WHERE StockRack > 0 OR StockPackage > 0
                ORDER BY StockRack, StockPackage";
            using var migR = migCmd.ExecuteReader();
            var inserts = new List<(int Rack, int Pkg)>();
            while (migR.Read())
                inserts.Add((I(migR, "StockRack"), I(migR, "StockPackage")));

            foreach (var (rack, pkg) in inserts)
            {
                string code = $"R{rack}" + (pkg > 0 ? $"-P{pkg}" : "");
                using var ins = c.CreateCommand();
                ins.CommandText = "INSERT OR IGNORE INTO Locations (Code, Description) VALUES (@code, @desc)";
                ins.Parameters.AddWithValue("@code", code);
                ins.Parameters.AddWithValue("@desc", code);
                ins.ExecuteNonQuery();

                using var upd = c.CreateCommand();
                upd.CommandText = @"
                    UPDATE Components SET LocationId = (
                        SELECT Id FROM Locations WHERE Code = @code
                    ) WHERE StockRack = @rack AND StockPackage = @pkg";
                upd.Parameters.AddWithValue("@code", code);
                upd.Parameters.AddWithValue("@rack", rack);
                upd.Parameters.AddWithValue("@pkg",  pkg);
                upd.ExecuteNonQuery();
            }
        }

        Exec(c, @"
            CREATE TABLE IF NOT EXISTS AppInfo (
                Id          INTEGER PRIMARY KEY CHECK (Id = 1),
                Name        TEXT NOT NULL DEFAULT 'MyHobbyWarehouse',
                Description TEXT NOT NULL DEFAULT '',
                LogoPath    TEXT NOT NULL DEFAULT ''
            );");
        // Insert default row if missing
        long existingInfo = Scalar<long>(c, "SELECT COUNT(*) FROM AppInfo");
        if (existingInfo == 0)
        {
            Exec(c, "INSERT INTO AppInfo (Id, Name, Description, LogoPath) VALUES (1, 'MyHobbyWarehouse', '', '')");
        }

        Exec(c, @"
            CREATE TABLE IF NOT EXISTS StockTransactions (
                Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                ComponentSku         TEXT NOT NULL DEFAULT '',
                ComponentDescription TEXT NOT NULL DEFAULT '',
                Type                 INTEGER NOT NULL DEFAULT 0,
                Date                 TEXT NOT NULL,
                Qty                  REAL NOT NULL DEFAULT 0,
                UnitPrice            REAL NOT NULL DEFAULT 0,
                Supplier             TEXT NOT NULL DEFAULT '',
                Notes                TEXT NOT NULL DEFAULT '',
                ProjectId            INTEGER,
                ProjectName          TEXT NOT NULL DEFAULT ''
            );");
    }

    // ── Components ───────────────────────────────────────────────────────────

    public List<Component> GetAllComponents(string? search = null, string? category1 = null)
    {
        var list = new List<Component>();
        using var c = Connect();
        using var cmd = c.CreateCommand();

        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            where.Add(@"(Description LIKE @s OR Sku LIKE @s OR OldSku LIKE @s
                         OR Category3 LIKE @s OR ManufacturerPart LIKE @s
                         OR StickerText LIKE @s)");
        if (!string.IsNullOrWhiteSpace(category1))
            where.Add("Category1 = @cat");

        cmd.CommandText = "SELECT * FROM Components"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY Sku";

        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@s", $"%{search}%");
        if (!string.IsNullOrWhiteSpace(category1))
            cmd.Parameters.AddWithValue("@cat", category1);

        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadComponent(r));
        PopulateLocationCodes(list);
        return list;
    }

    public Component? GetComponent(string sku)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Components WHERE Sku = @sku";
        cmd.Parameters.AddWithValue("@sku", sku);
        using var r = cmd.ExecuteReader();
        var comp = r.Read() ? ReadComponent(r) : null;
        if (comp != null) PopulateLocationCode(comp);
        return comp;
    }

    public bool ComponentExists(string sku)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Components WHERE Sku = @sku";
        cmd.Parameters.AddWithValue("@sku", sku);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>Returns the next available auto-increment SKU (e.g. "0001").</summary>
    public string GetNextSku()
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            SELECT Sku FROM Components
            WHERE Sku GLOB '[0-9]*'
            ORDER BY CAST(Sku AS INTEGER) DESC
            LIMIT 1";
        var result = cmd.ExecuteScalar();
        int next = 1;
        if (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int max))
            next = max + 1;
        return next.ToString("D4");
    }

    public void SaveComponent(Component comp)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Components (
                Sku,OldSku,Alt,Description,Unit,
                StockSum,Stock,StockRfu1,StockRfu2,StockRfu3,StockRfu4,
                StockRack,StockPackage,StockZone,LocationId,
                LastPrice,StockValue,SupCount,LastSupplier,LastSupplierSku,
                MassMg,Smd,
                Category1,Category2,Category3,Category4,Category5,
                ManufacturerName,ManufacturerPart,
                Supplier1Name,Supplier1Sku,Supplier1Price,
                Supplier2Name,Supplier2Sku,Supplier2Price,
                Supplier3Name,Supplier3Sku,Supplier3Price,
                Supplier1Url,Supplier2Url,Supplier3Url,
                StickerText
            ) VALUES (
                @Sku,@OldSku,@Alt,@Description,@Unit,
                @StockSum,@Stock,@StockRfu1,@StockRfu2,@StockRfu3,@StockRfu4,
                @StockRack,@StockPackage,@StockZone,@LocationId,
                @LastPrice,@StockValue,@SupCount,@LastSupplier,@LastSupplierSku,
                @MassMg,@Smd,
                @Cat1,@Cat2,@Cat3,@Cat4,@Cat5,
                @MfgName,@MfgPart,
                @S1Name,@S1Sku,@S1Price,
                @S2Name,@S2Sku,@S2Price,
                @S3Name,@S3Sku,@S3Price,
                @S1Url,@S2Url,@S3Url,
                @Sticker
            )
            ON CONFLICT(Sku) DO UPDATE SET
                OldSku=@OldSku, Alt=@Alt, Description=@Description, Unit=@Unit,
                StockSum=@StockSum, Stock=@Stock,
                StockRfu1=@StockRfu1,StockRfu2=@StockRfu2,
                StockRfu3=@StockRfu3,StockRfu4=@StockRfu4,
                StockRack=@StockRack,StockPackage=@StockPackage,StockZone=@StockZone,
                LocationId=@LocationId,
                LastPrice=@LastPrice,StockValue=@StockValue,
                SupCount=@SupCount,LastSupplier=@LastSupplier,LastSupplierSku=@LastSupplierSku,
                MassMg=@MassMg,Smd=@Smd,
                Category1=@Cat1,Category2=@Cat2,Category3=@Cat3,Category4=@Cat4,Category5=@Cat5,
                ManufacturerName=@MfgName,ManufacturerPart=@MfgPart,
                Supplier1Name=@S1Name,Supplier1Sku=@S1Sku,Supplier1Price=@S1Price,
                Supplier2Name=@S2Name,Supplier2Sku=@S2Sku,Supplier2Price=@S2Price,
                Supplier3Name=@S3Name,Supplier3Sku=@S3Sku,Supplier3Price=@S3Price,
                Supplier1Url=@S1Url,Supplier2Url=@S2Url,Supplier3Url=@S3Url,
                StickerText=@Sticker";
        BindComponent(cmd, comp);
        cmd.ExecuteNonQuery();
    }

    public void DeleteComponent(string sku)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Components WHERE Sku = @sku";
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Updates only StockSum and derived StockValue.</summary>
    public void UpdateStock(string sku, double newStock)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE Components
                            SET StockSum   = @s,
                                StockValue = @s * LastPrice
                            WHERE Sku = @sku";
        cmd.Parameters.AddWithValue("@s",   newStock);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetCategories()
    {
        var list = new List<string>();
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category1 FROM Components WHERE Category1 != '' ORDER BY Category1";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    // ── Projects ─────────────────────────────────────────────────────────────

    public List<Project> GetAllProjects()
    {
        var list = new List<Project>();
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Projects ORDER BY ModifiedDate DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadProject(r));
        return list;
    }

    public int SaveProject(Project p)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();

        if (p.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO Projects (Name,BoardName,Version,Revision,Description,Notes,CreatedDate,ModifiedDate)
                VALUES (@Name,@Board,@Ver,@Rev,@Desc,@Notes,@Created,@Modified);
                SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = @"
                UPDATE Projects SET Name=@Name,BoardName=@Board,Version=@Ver,Revision=@Rev,
                    Description=@Desc,Notes=@Notes,ModifiedDate=@Modified
                WHERE Id=@Id;
                SELECT @Id;";
            cmd.Parameters.AddWithValue("@Id", p.Id);
        }
        cmd.Parameters.AddWithValue("@Name",    p.Name);
        cmd.Parameters.AddWithValue("@Board",   p.BoardName);
        cmd.Parameters.AddWithValue("@Ver",     p.Version);
        cmd.Parameters.AddWithValue("@Rev",     p.Revision);
        cmd.Parameters.AddWithValue("@Desc",    p.Description);
        cmd.Parameters.AddWithValue("@Notes",   p.Notes);
        cmd.Parameters.AddWithValue("@Created", p.CreatedDate.ToString("o"));
        cmd.Parameters.AddWithValue("@Modified", DateTime.Now.ToString("o"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void DeleteProject(int id)
    {
        using var c = Connect();
        Exec(c, "DELETE FROM BomLines          WHERE ProjectId = " + id);
        // Remove transaction records linked to this project (does NOT change stock)
        Exec(c, "DELETE FROM StockTransactions WHERE ProjectId = " + id);
        Exec(c, "DELETE FROM Projects          WHERE Id       = " + id);
    }

    // ── BOM Lines ────────────────────────────────────────────────────────────

    public List<BomLine> GetBomLines(int projectId)
    {
        var list = new List<BomLine>();
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM BomLines WHERE ProjectId = @pid ORDER BY Id";
        cmd.Parameters.AddWithValue("@pid", projectId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadBomLine(r));
        return list;
    }

    /// <summary>Returns BOM lines with Component objects populated from library.</summary>
    public List<BomLine> GetBomLinesWithComponents(int projectId)
    {
        var lines = GetBomLines(projectId);
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line.Sku))
                line.Component = GetComponent(line.Sku);
            if (!string.IsNullOrEmpty(line.Sku2))
                line.Component2 = GetComponent(line.Sku2);
        }
        return lines;
    }

    /// <summary>Inserts a single new BOM line and returns its new Id.</summary>
    public int AddBomLine(BomLine line)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO BomLines
                (ProjectId,Sku,Sku2,Qty,Unit,PartDesignators,Value,Device,Package,Notes)
            VALUES
                (@pid,@Sku,@Sku2,@Qty,@Unit,@Parts,@Value,@Dev,@Pkg,@Notes);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid",   line.ProjectId);
        cmd.Parameters.AddWithValue("@Sku",   line.Sku);
        cmd.Parameters.AddWithValue("@Sku2",  line.Sku2);
        cmd.Parameters.AddWithValue("@Qty",   line.Qty);
        cmd.Parameters.AddWithValue("@Unit",  line.Unit);
        cmd.Parameters.AddWithValue("@Parts", line.PartDesignators);
        cmd.Parameters.AddWithValue("@Value", line.Value);
        cmd.Parameters.AddWithValue("@Dev",   line.Device);
        cmd.Parameters.AddWithValue("@Pkg",   line.Package);
        cmd.Parameters.AddWithValue("@Notes", line.Notes);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Updates an existing single BOM line.</summary>
    public void UpdateBomLine(BomLine line)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            UPDATE BomLines SET
                Sku=@Sku, Sku2=@Sku2, Qty=@Qty, Unit=@Unit,
                PartDesignators=@Parts, Value=@Value, Device=@Dev,
                Package=@Pkg, Notes=@Notes
            WHERE Id=@Id";
        cmd.Parameters.AddWithValue("@Id",    line.Id);
        cmd.Parameters.AddWithValue("@Sku",   line.Sku);
        cmd.Parameters.AddWithValue("@Sku2",  line.Sku2);
        cmd.Parameters.AddWithValue("@Qty",   line.Qty);
        cmd.Parameters.AddWithValue("@Unit",  line.Unit);
        cmd.Parameters.AddWithValue("@Parts", line.PartDesignators);
        cmd.Parameters.AddWithValue("@Value", line.Value);
        cmd.Parameters.AddWithValue("@Dev",   line.Device);
        cmd.Parameters.AddWithValue("@Pkg",   line.Package);
        cmd.Parameters.AddWithValue("@Notes", line.Notes);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Deletes a single BOM line by its Id.</summary>
    public void DeleteBomLine(int lineId)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM BomLines WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", lineId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Replaces all BOM lines for a project.</summary>
    public void SaveBomLines(int projectId, List<BomLine> lines)
    {
        using var c = Connect();
        Exec(c, $"DELETE FROM BomLines WHERE ProjectId = {projectId}");
        foreach (var l in lines)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO BomLines
                    (ProjectId,Sku,Sku2,Qty,Unit,PartDesignators,Value,Device,Package,Notes)
                VALUES
                    (@pid,@Sku,@Sku2,@Qty,@Unit,@Parts,@Value,@Dev,@Pkg,@Notes)";
            cmd.Parameters.AddWithValue("@pid",   projectId);
            cmd.Parameters.AddWithValue("@Sku",   l.Sku);
            cmd.Parameters.AddWithValue("@Sku2",  l.Sku2);
            cmd.Parameters.AddWithValue("@Qty",   l.Qty);
            cmd.Parameters.AddWithValue("@Unit",  l.Unit);
            cmd.Parameters.AddWithValue("@Parts", l.PartDesignators);
            cmd.Parameters.AddWithValue("@Value", l.Value);
            cmd.Parameters.AddWithValue("@Dev",   l.Device);
            cmd.Parameters.AddWithValue("@Pkg",   l.Package);
            cmd.Parameters.AddWithValue("@Notes", l.Notes);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Build ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deducts stock for all BOM lines of a project (multiplied by qty of boards).
    /// Records a BuildUse transaction for every component used.
    /// Returns list of warnings (components with insufficient stock).
    /// </summary>
    public List<string> BuildProject(Project project, int boards = 1, string notes = "")
    {
        var warnings = new List<string>();
        var lines = GetBomLines(project.Id);

        using var c = Connect();
        var now = DateTime.Now;

        foreach (var line in lines)
        {
            // SKU2 is an alternative only - never deducted from stock
            DeductLine(c, line.Sku, line.Qty * boards, project, now, notes, warnings);
        }
        return warnings;
    }

    private void DeductLine(SqliteConnection c, string sku, double needed,
        Project project, DateTime now, string notes, List<string> warnings)
    {
        if (string.IsNullOrEmpty(sku)) return;

        // Read current stock on THE SAME connection to avoid stale reads
        double before = 0;
        string desc   = "";
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT StockSum, Description FROM Components WHERE Sku = @sku";
            cmd.Parameters.AddWithValue("@sku", sku);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) { warnings.Add(TranslationService.Get("BuildWarnSkuNotInLib", sku)); return; }
            before = r.IsDBNull(0) ? 0 : r.GetDouble(0);
            desc   = r.IsDBNull(1) ? sku : r.GetString(1);
        }

        double after = Math.Max(0, before - needed);
        if (before < needed)
            warnings.Add(TranslationService.Get("BuildWarnShortage", sku, desc, needed, before));

        UpdateStockConn(c, sku, after);

        var tx = new StockTransaction
        {
            ComponentSku         = sku,
            ComponentDescription = desc,
            Type                 = TransactionType.BuildUse,
            Date                 = now,
            Qty                  = -needed,
            Notes                = notes,
            ProjectId            = project.Id,
            ProjectName          = project.DisplayName,
        };
        InsertTransaction(c, tx);
    }

    private void UpdateStockConn(SqliteConnection c, string sku, double newStock)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE Components SET StockSum=@s, StockValue=@s*LastPrice WHERE Sku=@sku";
        cmd.Parameters.AddWithValue("@s",   newStock);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.ExecuteNonQuery();
    }

    // ── Transactions ─────────────────────────────────────────────────────────

    public void AddTransaction(StockTransaction tx)
    {
        using var c = Connect();
        InsertTransaction(c, tx);

        // Adjust stock according to qty sign
        var comp = GetComponent(tx.ComponentSku);
        if (comp != null)
            UpdateStock(tx.ComponentSku, comp.StockSum + tx.Qty);
    }

    private void InsertTransaction(SqliteConnection c, StockTransaction tx)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO StockTransactions
                (ComponentSku,ComponentDescription,Type,Date,Qty,UnitPrice,Supplier,Notes,ProjectId,ProjectName)
            VALUES
                (@Sku,@Desc,@Type,@Date,@Qty,@Price,@Sup,@Notes,@ProjId,@ProjName)";
        cmd.Parameters.AddWithValue("@Sku",      tx.ComponentSku);
        cmd.Parameters.AddWithValue("@Desc",     tx.ComponentDescription);
        cmd.Parameters.AddWithValue("@Type",     (int)tx.Type);
        cmd.Parameters.AddWithValue("@Date",     tx.Date.ToString("o"));
        cmd.Parameters.AddWithValue("@Qty",      tx.Qty);
        cmd.Parameters.AddWithValue("@Price",    tx.UnitPrice);
        cmd.Parameters.AddWithValue("@Sup",      tx.Supplier);
        cmd.Parameters.AddWithValue("@Notes",    tx.Notes);
        cmd.Parameters.AddWithValue("@ProjId",   tx.ProjectId.HasValue ? (object)tx.ProjectId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ProjName", tx.ProjectName);
        cmd.ExecuteNonQuery();
    }

    public List<StockTransaction> GetTransactions(string? sku = null, int? projectId = null)
    {
        var list  = new List<StockTransaction>();
        var where = new List<string>();
        if (!string.IsNullOrEmpty(sku))       where.Add("ComponentSku = @sku");
        if (projectId.HasValue)               where.Add("ProjectId    = @pid");

        using var c   = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM StockTransactions"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY Date DESC";
        if (!string.IsNullOrEmpty(sku))  cmd.Parameters.AddWithValue("@sku", sku);
        if (projectId.HasValue)          cmd.Parameters.AddWithValue("@pid", projectId.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadTransaction(r));
        return list;
    }

    // ── Statistics ───────────────────────────────────────────────────────────

    public (double TotalValue, int ComponentTypes, int TotalStock, int ProjectCount) GetStats()
    {
        using var c = Connect();
        double totalValue = Scalar<double>(c, "SELECT COALESCE(SUM(StockValue),0) FROM Components");
        int    compTypes  = Scalar<int>   (c, "SELECT COUNT(*) FROM Components");
        int    totalStock = Scalar<int>   (c, "SELECT COALESCE(SUM(StockSum),0) FROM Components");
        int    projects   = Scalar<int>   (c, "SELECT COUNT(*) FROM Projects");
        return (totalValue, compTypes, totalStock, projects);
    }

    // ── Readers ──────────────────────────────────────────────────────────────

    private static Component ReadComponent(SqliteDataReader r) => new()
    {
        Sku             = S(r,"Sku"),
        OldSku          = S(r,"OldSku"),
        Alt             = S(r,"Alt"),
        Description     = S(r,"Description"),
        Unit            = S(r,"Unit","pcs"),
        StockSum        = D(r,"StockSum"),
        Stock           = D(r,"Stock"),
        StockRfu1       = D(r,"StockRfu1"),
        StockRfu2       = D(r,"StockRfu2"),
        StockRfu3       = D(r,"StockRfu3"),
        StockRfu4       = D(r,"StockRfu4"),
        StockRack       = I(r,"StockRack"),
        StockPackage    = I(r,"StockPackage"),
        StockZone       = I(r,"StockZone"),
        LocationId      = r["LocationId"] == DBNull.Value ? null : I(r,"LocationId"),
        LastPrice       = D(r,"LastPrice"),
        StockValue      = D(r,"StockValue"),
        SupCount        = I(r,"SupCount"),
        LastSupplier    = S(r,"LastSupplier"),
        LastSupplierSku = S(r,"LastSupplierSku"),
        MassMg          = D(r,"MassMg"),
        Smd             = I(r,"Smd") == 1,
        Category1       = S(r,"Category1"),
        Category2       = S(r,"Category2"),
        Category3       = S(r,"Category3"),
        Category4       = S(r,"Category4"),
        Category5       = S(r,"Category5"),
        ManufacturerName = S(r,"ManufacturerName"),
        ManufacturerPart = S(r,"ManufacturerPart"),
        Supplier1Name   = S(r,"Supplier1Name"),
        Supplier1Sku    = S(r,"Supplier1Sku"),
        Supplier1Price  = D(r,"Supplier1Price"),
        Supplier2Name   = S(r,"Supplier2Name"),
        Supplier2Sku    = S(r,"Supplier2Sku"),
        Supplier2Price  = D(r,"Supplier2Price"),
        Supplier3Name   = S(r,"Supplier3Name"),
        Supplier3Sku    = S(r,"Supplier3Sku"),
        Supplier3Price  = D(r,"Supplier3Price"),
        Supplier1Url    = S(r,"Supplier1Url"),
        Supplier2Url    = S(r,"Supplier2Url"),
        Supplier3Url    = S(r,"Supplier3Url"),
        StickerText     = S(r,"StickerText"),
    };

    private static Project ReadProject(SqliteDataReader r) => new()
    {
        Id           = I(r,"Id"),
        Name         = S(r,"Name"),
        BoardName    = S(r,"BoardName"),
        Version      = S(r,"Version"),
        Revision     = S(r,"Revision"),
        Description  = S(r,"Description"),
        Notes        = S(r,"Notes"),
        CreatedDate  = DateTime.Parse(S(r,"CreatedDate")),
        ModifiedDate = DateTime.Parse(S(r,"ModifiedDate")),
    };

    private static BomLine ReadBomLine(SqliteDataReader r) => new()
    {
        Id              = I(r,"Id"),
        ProjectId       = I(r,"ProjectId"),
        Sku             = S(r,"Sku"),
        Sku2            = S(r,"Sku2"),
        Qty             = D(r,"Qty"),
        Unit            = S(r,"Unit","pcs"),
        PartDesignators = S(r,"PartDesignators"),
        Value           = S(r,"Value"),
        Device          = S(r,"Device"),
        Package         = S(r,"Package"),
        Notes           = S(r,"Notes"),
    };

    private static StockTransaction ReadTransaction(SqliteDataReader r) => new()
    {
        Id                   = I(r,"Id"),
        ComponentSku         = S(r,"ComponentSku"),
        ComponentDescription = S(r,"ComponentDescription"),
        Type                 = (TransactionType)I(r,"Type"),
        Date                 = DateTime.Parse(S(r,"Date")),
        Qty                  = D(r,"Qty"),
        UnitPrice            = D(r,"UnitPrice"),
        Supplier             = S(r,"Supplier"),
        Notes                = S(r,"Notes"),
        ProjectId            = r["ProjectId"] == DBNull.Value ? null : I(r,"ProjectId"),
        ProjectName          = S(r,"ProjectName"),
    };

    // ── Bind helpers ─────────────────────────────────────────────────────────

    private static void BindComponent(SqliteCommand cmd, Component x)
    {
        cmd.Parameters.AddWithValue("@Sku",     x.Sku);
        cmd.Parameters.AddWithValue("@OldSku",  x.OldSku);
        cmd.Parameters.AddWithValue("@Alt",     x.Alt);
        cmd.Parameters.AddWithValue("@Description", x.Description);
        cmd.Parameters.AddWithValue("@Unit",    x.Unit);
        cmd.Parameters.AddWithValue("@StockSum",x.StockSum);
        cmd.Parameters.AddWithValue("@Stock",   x.Stock);
        cmd.Parameters.AddWithValue("@StockRfu1",x.StockRfu1);
        cmd.Parameters.AddWithValue("@StockRfu2",x.StockRfu2);
        cmd.Parameters.AddWithValue("@StockRfu3",x.StockRfu3);
        cmd.Parameters.AddWithValue("@StockRfu4",x.StockRfu4);
        cmd.Parameters.AddWithValue("@StockRack",    x.StockRack);
        cmd.Parameters.AddWithValue("@StockPackage", x.StockPackage);
        cmd.Parameters.AddWithValue("@StockZone",    x.StockZone);
        cmd.Parameters.AddWithValue("@LocationId",  (object?)x.LocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastPrice",    x.LastPrice);
        cmd.Parameters.AddWithValue("@StockValue",   x.StockValue);
        cmd.Parameters.AddWithValue("@SupCount",     x.SupCount);
        cmd.Parameters.AddWithValue("@LastSupplier", x.LastSupplier);
        cmd.Parameters.AddWithValue("@LastSupplierSku", x.LastSupplierSku);
        cmd.Parameters.AddWithValue("@MassMg",  x.MassMg);
        cmd.Parameters.AddWithValue("@Smd",     x.Smd ? 1 : 0);
        cmd.Parameters.AddWithValue("@Cat1",    x.Category1);
        cmd.Parameters.AddWithValue("@Cat2",    x.Category2);
        cmd.Parameters.AddWithValue("@Cat3",    x.Category3);
        cmd.Parameters.AddWithValue("@Cat4",    x.Category4);
        cmd.Parameters.AddWithValue("@Cat5",    x.Category5);
        cmd.Parameters.AddWithValue("@MfgName", x.ManufacturerName);
        cmd.Parameters.AddWithValue("@MfgPart", x.ManufacturerPart);
        cmd.Parameters.AddWithValue("@S1Name",  x.Supplier1Name);
        cmd.Parameters.AddWithValue("@S1Sku",   x.Supplier1Sku);
        cmd.Parameters.AddWithValue("@S1Price", x.Supplier1Price);
        cmd.Parameters.AddWithValue("@S2Name",  x.Supplier2Name);
        cmd.Parameters.AddWithValue("@S2Sku",   x.Supplier2Sku);
        cmd.Parameters.AddWithValue("@S2Price", x.Supplier2Price);
        cmd.Parameters.AddWithValue("@S3Name",  x.Supplier3Name);
        cmd.Parameters.AddWithValue("@S3Sku",   x.Supplier3Sku);
        cmd.Parameters.AddWithValue("@S3Price", x.Supplier3Price);
        cmd.Parameters.AddWithValue("@S1Url",   x.Supplier1Url);
        cmd.Parameters.AddWithValue("@S2Url",   x.Supplier2Url);
        cmd.Parameters.AddWithValue("@S3Url",   x.Supplier3Url);
        cmd.Parameters.AddWithValue("@Sticker", x.StickerText);
    }

    // ── Locations ────────────────────────────────────────────────────────────

    public List<Location> GetAllLocations()
    {
        var list = new List<Location>();
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Locations ORDER BY Code";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadLocation(r));
        return list;
    }

    public Location? GetLocation(int id)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Locations WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadLocation(r) : null;
    }

    public int SaveLocation(Location loc)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        if (loc.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Locations (Code, Description) VALUES (@code, @desc); SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = "UPDATE Locations SET Code=@code, Description=@desc WHERE Id=@id; SELECT @id;";
            cmd.Parameters.AddWithValue("@id", loc.Id);
        }
        cmd.Parameters.AddWithValue("@code", loc.Code);
        cmd.Parameters.AddWithValue("@desc", loc.Description);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetComponentCountForLocation(int locationId)
    {
        using var c = Connect();
        return Scalar<int>(c, $"SELECT COUNT(*) FROM Components WHERE LocationId = {locationId}");
    }

    public void DeleteLocation(int id)
    {
        using var c = Connect();
        Exec(c, $"UPDATE Components SET LocationId = NULL WHERE LocationId = {id}");
        Exec(c, $"DELETE FROM Locations WHERE Id = {id}");
    }

    private static Location ReadLocation(SqliteDataReader r) => new()
    {
        Id          = I(r, "Id"),
        Code        = S(r, "Code"),
        Description = S(r, "Description"),
    };

    private Dictionary<int, string> GetLocationCodeMap()
    {
        var dict = new Dictionary<int, string>();
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, Code FROM Locations";
        using var r = cmd.ExecuteReader();
        while (r.Read()) dict[I(r, "Id")] = S(r, "Code");
        return dict;
    }

    private void PopulateLocationCodes(List<Component> list)
    {
        var locMap = GetLocationCodeMap();
        foreach (var comp in list)
        {
            if (comp.LocationId.HasValue && locMap.TryGetValue(comp.LocationId.Value, out var code))
                comp.LocationCode = code;
        }
    }

    private void PopulateLocationCode(Component comp)
    {
        if (!comp.LocationId.HasValue) return;
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Code FROM Locations WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", comp.LocationId.Value);
        var code = cmd.ExecuteScalar();
        comp.LocationCode = code?.ToString() ?? "";
    }

    // ── Tiny helpers ─────────────────────────────────────────────────────────

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    private static string  S(SqliteDataReader r, string col, string def = "")
        => r[col] == DBNull.Value ? def : (r[col]?.ToString() ?? def);
    private static double  D(SqliteDataReader r, string col)
        => r[col] == DBNull.Value ? 0.0 : Convert.ToDouble(r[col]);
    private static int     I(SqliteDataReader r, string col)
        => r[col] == DBNull.Value ? 0   : Convert.ToInt32(r[col]);
    // ── Backup ──────────────────────────────────────────────────────────────

    /// <summary>Creates a timestamped backup copy of the database file.</summary>
    public string Backup()
    {
        string src        = _cs.Replace("Data Source=", "").Trim();
        string backupDir  = Path.Combine(Path.GetDirectoryName(src)!, "Backups");
        Directory.CreateDirectory(backupDir);
        string ts  = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string dst = Path.Combine(backupDir, $"myhobbywarehouse_backup_{ts}.db");
        File.Copy(src, dst, overwrite: false);
        return dst;
    }

    public List<string> GetBackups()
    {
        string src       = _cs.Replace("Data Source=", "").Trim();
        string backupDir = Path.Combine(Path.GetDirectoryName(src)!, "Backups");
        if (!Directory.Exists(backupDir)) return [];
        return Directory.GetFiles(backupDir, "myhobbywarehouse_backup_*.db")
            .OrderByDescending(f => f).ToList();
    }
    // ── Reverse transaction ─────────────────────────────────────────────────

    /// <summary>Creates a reverse (undo) transaction: opposite qty, adds back to stock.</summary>
    public void ReverseTransaction(StockTransaction original)
    {
        var reverse = new StockTransaction
        {
            ComponentSku         = original.ComponentSku,
            ComponentDescription = original.ComponentDescription,
            Type                 = TransactionType.Adjustment,
            Date                 = DateTime.Now,
            Qty                  = -original.Qty,
            UnitPrice            = original.UnitPrice,
            Supplier             = original.Supplier,
            Notes                = $"REDO #{ original.Id} [{original.DisplayDate}]",
            ProjectId            = original.ProjectId,
            ProjectName          = original.ProjectName,
        };
        AddTransaction(reverse);
    }
    // ── Transaction groups ───────────────────────────────────────────────────

    /// <summary>
    /// Returns transactions grouped by Notes tag.
    /// One row per unique build/tag.
    /// </summary>
    public List<TransactionGroup> GetTransactionGroups(string? filter = null)
    {
        var list = new List<TransactionGroup>();
        using var c = Connect();
        using var cmd = c.CreateCommand();

        string where = string.IsNullOrWhiteSpace(filter)
            ? ""
            : "WHERE Notes LIKE @f OR ProjectName LIKE @f";

        cmd.CommandText = $@"
            SELECT
                Notes,
                ProjectName,
                MIN(Date)      AS FirstDate,
                COUNT(*)       AS Cnt,
                SUM(ABS(Qty))  AS TotalQty,
                Type
            FROM StockTransactions
            {where}
            GROUP BY Notes
            ORDER BY FirstDate DESC";

        if (!string.IsNullOrWhiteSpace(filter))
            cmd.Parameters.AddWithValue("@f", $"%{filter}%");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new TransactionGroup
            {
                Tag              = r["Notes"]?.ToString() ?? "",
                ProjectName      = r["ProjectName"]?.ToString() ?? "",
                Date             = DateTime.Parse(r["FirstDate"].ToString()!),
                TransactionCount = Convert.ToInt32(r["Cnt"]),
                TotalQtyAbs      = Convert.ToDouble(r["TotalQty"]),
                PrimaryType      = (TransactionType)Convert.ToInt32(r["Type"]),
            });
        }
        return list;
    }

    /// <summary>Returns all individual transactions for a given Notes tag.</summary>
    public List<StockTransaction> GetTransactionsByTag(string tag)
        => GetTransactions().Where(t => t.Notes == tag).ToList();

    /// <summary>Reverses ALL transactions with the given Notes tag.</summary>
    public int ReverseTransactionGroup(string tag)
    {
        var txList = GetTransactionsByTag(tag);
        string newTag = $"REDO [{DateTime.Now:yyyy-MM-dd HH:mm}] {tag}";
        using var c = Connect();
        foreach (var tx in txList)
        {
            var rev = new StockTransaction
            {
                ComponentSku         = tx.ComponentSku,
                ComponentDescription = tx.ComponentDescription,
                Type                 = TransactionType.Adjustment,
                Date                 = DateTime.Now,
                Qty                  = -tx.Qty,
                UnitPrice            = tx.UnitPrice,
                Supplier             = tx.Supplier,
                Notes                = newTag,
                ProjectId            = tx.ProjectId,
                ProjectName          = tx.ProjectName,
            };
            InsertTransaction(c, rev);
            // Update stock
            double current = 0;
            using (var cmd2 = c.CreateCommand())
            {
                cmd2.CommandText = "SELECT StockSum FROM Components WHERE Sku=@s";
                cmd2.Parameters.AddWithValue("@s", tx.ComponentSku);
                var val = cmd2.ExecuteScalar();
                if (val != null && val != DBNull.Value)
                    current = Convert.ToDouble(val);
            }
            UpdateStockConn(c, tx.ComponentSku, Math.Max(0, current + (-tx.Qty)));
        }
        return txList.Count;
    }
    public void DeleteTransactionGroup(string tag)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM StockTransactions WHERE Notes = @tag";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.ExecuteNonQuery();
    }

    // ── App Info ──────────────────────────────────────────────────────────────

    public Models.AppInfo GetAppInfo()
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, LogoPath FROM AppInfo WHERE Id = 1";
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return new Models.AppInfo
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Description = r.GetString(2),
                LogoPath = r.GetString(3)
            };
        }
        return new Models.AppInfo();
    }

    public void SaveAppInfo(Models.AppInfo info)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            UPDATE AppInfo SET Name = @name, Description = @desc, LogoPath = @logo
            WHERE Id = 1";
        cmd.Parameters.AddWithValue("@name", info.Name);
        cmd.Parameters.AddWithValue("@desc", info.Description);
        cmd.Parameters.AddWithValue("@logo", info.LogoPath);
        cmd.ExecuteNonQuery();
    }

}
