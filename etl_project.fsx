// ============================================================================
// ETL Project - Functional Programming
// Aluno: Raphael Cavalcanti Banov
// Email: raphaelb3@al.insper.edu.br
// ============================================================================

open System
open System.IO

// ============================================================================
// SECTION 1: RECORD TYPES
// ============================================================================

/// <summary>
/// Represents an Order from the Order table.
/// </summary>
type Order = {
    id: int
    client_id: int
    order_date: DateTime
    status: string
    origin: string
}

/// <summary>
/// Represents an OrderItem from the OrderItem table.
/// </summary>
type OrderItem = {
    order_id: int
    product_id: int
    quantity: float
    price: float
    tax: float
}

/// <summary>
/// Represents the aggregated result for output containing order totals.
/// </summary>
type OrderSummary = {
    order_id: int
    total_amount: float
    total_taxes: float
}

/// <summary>
/// Represents monthly and yearly aggregated statistics for orders.
/// </summary>
type MonthlySummary = {
    year: int
    month: int
    average_amount: float
    average_taxes: float
    order_count: int
}

// ============================================================================
// SECTION 2: PURE HELPER FUNCTIONS FOR PARSING
// ============================================================================

/// <summary>
/// Parses a string to an integer value.
/// </summary>
let parseIntOption (str: string) : int option =
    match Int32.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Parses a string to a floating-point number.
/// </summary>
let parseFloatOption (str: string) : float option =
    match Double.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Parses a string to a DateTime value.
/// </summary>
let parseDateTimeOption (str: string) : DateTime option =
    match DateTime.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Trims leading and trailing whitespace from a string.
/// </summary>
let trim (str: string) : string =
    str.Trim()

/// <summary>
/// Splits a CSV line by comma and trims each resulting field.
/// </summary>
let splitCsvLine (line: string) : string array =
    line.Split(',') |> Array.map trim

/// <summary>
/// Converts a CSV line to an Order record.
/// </summary>
let lineToOrder (line: string) : Order option =
    let fields = splitCsvLine line
    if fields.Length < 5 then None
    else
        match (parseIntOption fields.[0], parseIntOption fields.[1], parseDateTimeOption fields.[2]) with
        | (Some id, Some client_id, Some order_date) ->
            Some {
                id = id
                client_id = client_id
                order_date = order_date
                status = fields.[3]
                origin = fields.[4]
            }
        | _ -> None

/// <summary>
/// Converts a CSV line to an OrderItem record.
/// </summary>
let lineToOrderItem (line: string) : OrderItem option =
    let fields = splitCsvLine line
    if fields.Length < 5 then None
    else
        match (parseIntOption fields.[0], parseIntOption fields.[1], parseFloatOption fields.[2], 
               parseFloatOption fields.[3], parseFloatOption fields.[4]) with
        | (Some order_id, Some product_id, Some quantity, Some price, Some tax) ->
            Some {
                order_id = order_id
                product_id = product_id
                quantity = quantity
                price = price
                tax = tax
            }
        | _ -> None

// ============================================================================
// SECTION 3: PURE TRANSFORMATION FUNCTIONS
// ============================================================================

/// <summary>
/// Calculates the revenue for a single OrderItem.
/// </summary>
let calculateItemRevenue (item: OrderItem) : float =
    item.quantity * item.price

/// <summary>
/// Calculates the tax amount for a single OrderItem.
/// </summary>
let calculateItemTax (item: OrderItem) : float =
    (calculateItemRevenue item) * item.tax

/// <summary>
/// Filters orders by status and origin criteria.
/// </summary>
let filterOrdersByStatusAndOrigin (status: string option) (origin: string option) (order: Order) : bool =
    let statusMatch = match status with
                      | None -> true
                      | Some s -> order.status.ToLower() = s.ToLower()
    let originMatch = match origin with
                      | None -> true
                      | Some o -> order.origin.ToLower() = o.ToLower()
    statusMatch && originMatch

/// <summary>
/// Performs an inner join between orders and order items.
/// This is PHASE 2 of the ETL pipeline: explicit join operation.
/// </summary>
/// <remarks>
/// This function implements a functional inner join using List.collect.
/// It returns tuples of (Order, OrderItem) for matching order_ids.
/// Orders with no items are excluded (true inner join semantics).
/// This satisfies Requisito Opcional 3: Inner Join in F#.
/// </remarks>
let joinOrdersWithItems (orders: Order list) (items: OrderItem list) : (Order * OrderItem) list =
    orders
    |> List.collect (fun order ->
        items
        |> List.filter (fun item -> item.order_id = order.id)
        |> List.map (fun item -> (order, item))
    )

/// <summary>
/// Transforms joined data into aggregated order summaries.
/// This is PHASE 3 of the ETL pipeline: transformation and aggregation.
/// </summary>
/// <remarks>
/// This function takes the output of the join and aggregates it by order_id,
/// calculating total amounts and taxes for each order.
/// </remarks>
let transformJoinedData (joinedData: (Order * OrderItem) list) : OrderSummary list =
    joinedData
    |> List.groupBy (fun (order, _) -> order.id)
    |> List.map (fun (orderId, group) ->
        let totalAmount = 
            group
            |> List.map (fun (_, item) -> calculateItemRevenue item)
            |> List.fold (+) 0.0
        
        let totalTaxes = 
            group
            |> List.map (fun (_, item) -> calculateItemTax item)
            |> List.fold (+) 0.0
        
        {
            order_id = orderId
            total_amount = totalAmount
            total_taxes = totalTaxes
        }
    )

/// <summary>
/// Processes the complete ETL pipeline for order aggregation.
/// Orchestrates all three phases: Filter, Join, and Transform.
/// </summary>
/// <remarks>
/// This function demonstrates the three-phase ETL approach:
/// Phase 1: Filter orders based on criteria
/// Phase 2: Perform inner join with items (Requisito Opcional 3)
/// Phase 3: Transform and aggregate the joined data
/// </remarks>
let processETL (orders: Order list) (items: OrderItem list) (statusFilter: string option) (originFilter: string option) : OrderSummary list =
    // Phase 1: Load & Filter Orders
    let filteredOrders = 
        orders
        |> List.filter (filterOrdersByStatusAndOrigin statusFilter originFilter)
    
    // Phase 2: Inner Join (EXPLICIT - Requisito Opcional 3)
    let joinedData = 
        joinOrdersWithItems filteredOrders items
    
    // Phase 3: Transform & Aggregate
    let summaries = 
        transformJoinedData joinedData
    
    summaries
    |> List.sortBy (fun summary -> summary.order_id)

/// <summary>
/// Calculates monthly and yearly aggregated statistics from order summaries.
/// </summary>
let calculateMonthlySummaries (orders: Order list) (summaries: OrderSummary list) : MonthlySummary list =
    let orderMap = orders |> List.map (fun o -> (o.id, o)) |> Map.ofList
    
    summaries
    |> List.choose (fun summary ->
        match Map.tryFind summary.order_id orderMap with
        | Some order ->
            Some (order.order_date.Year, order.order_date.Month, summary)
        | None -> None
    )
    |> List.groupBy (fun (year, month, _) -> (year, month))
    |> List.map (fun ((year, month), group) ->
        let count = group.Length
        let totalAmount = group |> List.sumBy (fun (_, _, s) -> s.total_amount)
        let totalTaxes = group |> List.sumBy (fun (_, _, s) -> s.total_taxes)
        
        {
            year = year
            month = month
            average_amount = totalAmount / float count
            average_taxes = totalTaxes / float count
            order_count = count
        }
    )
    |> List.sortBy (fun m -> (m.year, m.month))

// ============================================================================
// SECTION 4: IMPURE I/O FUNCTIONS - CSV
// ============================================================================

/// <summary>
/// Reads a CSV file and returns a list of lines, excluding the header.
/// </summary>
let readCsvFile (filePath: string) : string list =
    try
        File.ReadAllLines(filePath)
        |> Array.toList
        |> function
            | [] -> []
            | _ :: tail -> tail
    with
    | :? FileNotFoundException -> 
        printfn "Error: File not found: %s" filePath
        []
    | ex ->
        printfn "Error reading file %s: %s" filePath ex.Message
        []

/// <summary>
/// Loads Order records from a CSV file.
/// </summary>
let loadOrders (filePath: string) : Order list =
    readCsvFile filePath
    |> List.choose lineToOrder

/// <summary>
/// Loads OrderItem records from a CSV file.
/// </summary>
let loadOrderItems (filePath: string) : OrderItem list =
    readCsvFile filePath
    |> List.choose lineToOrderItem

/// <summary>
/// Converts an OrderSummary record to a CSV line.
/// </summary>
let orderSummaryToCsvLine (summary: OrderSummary) : string =
    sprintf "%d,%.2f,%.2f" summary.order_id summary.total_amount summary.total_taxes

/// <summary>
/// Converts a MonthlySummary record to a CSV line.
/// </summary>
let monthlySummaryToCsvLine (summary: MonthlySummary) : string =
    sprintf "%04d-%02d,%.2f,%.2f,%d" summary.year summary.month summary.average_amount summary.average_taxes summary.order_count

/// <summary>
/// Writes OrderSummary records to a CSV file.
/// </summary>
let writeResultsToCsv (filePath: string) (summaries: OrderSummary list) : unit =
    try
        let header = "order_id,total_amount,total_taxes"
        let lines = header :: (summaries |> List.map orderSummaryToCsvLine)
        File.WriteAllLines(filePath, lines)
        printfn "Results written to: %s" filePath
    with
    | ex ->
        printfn "Error writing to file %s: %s" filePath ex.Message

/// <summary>
/// Writes MonthlySummary records to a CSV file.
/// </summary>
let writeMonthlySummariesToCsv (filePath: string) (summaries: MonthlySummary list) : unit =
    try
        let header = "year-month,average_amount,average_taxes,order_count"
        let lines = header :: (summaries |> List.map monthlySummaryToCsvLine)
        File.WriteAllLines(filePath, lines)
        printfn "Monthly summaries written to: %s" filePath
    with
    | ex ->
        printfn "Error writing to file %s: %s" filePath ex.Message

// ============================================================================
// SECTION 5: IMPURE I/O FUNCTIONS - DATABASE (Requisito Opcional 2)
// ============================================================================

/// <summary>
/// Initializes the SQLite database and creates tables if they don't exist.
/// This function is idempotent and safe to call multiple times.
/// Uses sqlite3 command-line tool for database operations.
/// </summary>
/// <remarks>
/// Creates two tables:
/// - OrderSummaries: Stores aggregated order data
/// - MonthlySummaries: Stores monthly aggregated statistics
/// </remarks>
let initializeDatabase (dbPath: string) : unit =
    try
        let sqlCommands = """
CREATE TABLE IF NOT EXISTS OrderSummaries (
    order_id INTEGER PRIMARY KEY,
    total_amount REAL NOT NULL,
    total_taxes REAL NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS MonthlySummaries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    average_amount REAL NOT NULL,
    average_taxes REAL NOT NULL,
    order_count INTEGER NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
"""
        
        // Write SQL commands to a temporary file
        let tempSqlFile = Path.Combine(Path.GetTempPath(), "etl_init.sql")
        File.WriteAllText(tempSqlFile, sqlCommands)
        
        // Execute SQL commands using sqlite3 CLI
        let processInfo = System.Diagnostics.ProcessStartInfo()
        processInfo.FileName <- "/bin/bash"
        processInfo.Arguments <- sprintf "-c \"sqlite3 '%s' < '%s'\"" dbPath tempSqlFile
        processInfo.UseShellExecute <- false
        processInfo.RedirectStandardOutput <- true
        processInfo.RedirectStandardError <- true
        
        let proc = System.Diagnostics.Process.Start(processInfo)
        proc.WaitForExit()
        
        // Clean up temporary file
        File.Delete(tempSqlFile)
        
        printfn "Database initialized: %s" dbPath
    with
    | ex ->
        printfn "Error initializing database: %s" ex.Message

/// <summary>
/// Saves OrderSummary records to the database using sqlite3 CLI.
/// </summary>
let saveOrderSummariesToDatabase (dbPath: string) (summaries: OrderSummary list) : unit =
    try
        let sqlCommands = 
            "DELETE FROM OrderSummaries;\n" +
            (summaries
            |> List.map (fun s -> 
                sprintf "INSERT INTO OrderSummaries (order_id, total_amount, total_taxes) VALUES (%d, %.2f, %.2f);"
                    s.order_id s.total_amount s.total_taxes)
            |> String.concat "\n")
        
        let tempSqlFile = Path.Combine(Path.GetTempPath(), "etl_insert_orders.sql")
        File.WriteAllText(tempSqlFile, sqlCommands)
        
        let processInfo = System.Diagnostics.ProcessStartInfo()
        processInfo.FileName <- "/bin/bash"
        processInfo.Arguments <- sprintf "-c \"sqlite3 '%s' < '%s'\"" dbPath tempSqlFile
        processInfo.UseShellExecute <- false
        processInfo.RedirectStandardOutput <- true
        processInfo.RedirectStandardError <- true
        
        let proc = System.Diagnostics.Process.Start(processInfo)
        proc.WaitForExit()
        
        File.Delete(tempSqlFile)
        
        printfn "Saved %d order summaries to database" summaries.Length
    with
    | ex ->
        printfn "Error saving order summaries: %s" ex.Message

/// <summary>
/// Saves MonthlySummary records to the database using sqlite3 CLI.
/// </summary>
let saveMonthlySummariesToDatabase (dbPath: string) (summaries: MonthlySummary list) : unit =
    try
        let sqlCommands = 
            "DELETE FROM MonthlySummaries;\n" +
            (summaries
            |> List.map (fun s -> 
                sprintf "INSERT INTO MonthlySummaries (year, month, average_amount, average_taxes, order_count) VALUES (%d, %d, %.2f, %.2f, %d);"
                    s.year s.month s.average_amount s.average_taxes s.order_count)
            |> String.concat "\n")
        
        let tempSqlFile = Path.Combine(Path.GetTempPath(), "etl_insert_monthly.sql")
        File.WriteAllText(tempSqlFile, sqlCommands)
        
        let processInfo = System.Diagnostics.ProcessStartInfo()
        processInfo.FileName <- "/bin/bash"
        processInfo.Arguments <- sprintf "-c \"sqlite3 '%s' < '%s'\"" dbPath tempSqlFile
        processInfo.UseShellExecute <- false
        processInfo.RedirectStandardOutput <- true
        processInfo.RedirectStandardError <- true
        
        let proc = System.Diagnostics.Process.Start(processInfo)
        proc.WaitForExit()
        
        File.Delete(tempSqlFile)
        
        printfn "Saved %d monthly summaries to database" summaries.Length
    with
    | ex ->
        printfn "Error saving monthly summaries: %s" ex.Message

// ============================================================================
// SECTION 6: UTILITY FUNCTIONS
// ============================================================================

/// <summary>
/// Parses command line arguments into optional filter parameters.
/// </summary>
let parseArguments (args: string array) : (string option * string option) =
    match args.Length with
    | 0 -> (None, None)
    | 1 -> (Some args.[0], None)
    | _ -> (Some args.[0], Some args.[1])

// ============================================================================
// SECTION 7: MAIN PROGRAM
// ============================================================================

/// <summary>
/// Main entry point for the ETL program.
/// Orchestrates the complete ETL pipeline with database persistence.
/// </summary>
let main () =
    // Parse command line arguments
    let (statusFilter, originFilter) = parseArguments fsi.CommandLineArgs.[1..]
    
    // Print filter information
    match (statusFilter, originFilter) with
    | (None, None) -> printfn "Processing all orders (no filters applied)"
    | (Some status, None) -> printfn "Processing orders with status: %s" status
    | (None, Some origin) -> printfn "Processing orders with origin: %s" origin
    | (Some status, Some origin) -> printfn "Processing orders with status: %s and origin: %s" status origin
    
    // Initialize database (Requisito Opcional 2)
    printfn "\n--- Database Initialization ---"
    initializeDatabase "etl.db"
    
    // Load data from CSV files
    printfn "\n--- Loading Data ---"
    printfn "Loading orders from order.csv..."
    let orders = loadOrders "order.csv"
    printfn "Loaded %d orders" orders.Length
    
    printfn "Loading order items from order_item.csv..."
    let items = loadOrderItems "order_item.csv"
    printfn "Loaded %d order items" items.Length
    
    // Process ETL pipeline
    printfn "\n--- ETL Pipeline Execution (3 Phases) ---"
    printfn "Phase 1: Filtering orders..."
    printfn "Phase 2: Performing inner join (Requisito Opcional 3)..."
    printfn "Phase 3: Transforming and aggregating..."
    let results = processETL orders items statusFilter originFilter
    printfn "Generated %d order summaries" results.Length
    
    // Write results to CSV
    printfn "\n--- CSV Output ---"
    writeResultsToCsv "output.csv" results
    
    // Calculate and write monthly summaries
    printfn "Calculating monthly summaries..."
    let monthlySummaries = calculateMonthlySummaries orders results
    writeMonthlySummariesToCsv "monthly_summary.csv" monthlySummaries
    printfn "Generated %d monthly summaries" monthlySummaries.Length
    
    // Save to database (Requisito Opcional 2)
    printfn "\n--- Database Persistence (Requisito Opcional 2) ---"
    saveOrderSummariesToDatabase "etl.db" results
    saveMonthlySummariesToDatabase "etl.db" monthlySummaries
    
    // Print sample results
    printfn "\n--- Sample Results ---"
    printfn "Sample order summaries (first 5):"
    results
    |> List.take (min 5 results.Length)
    |> List.iter (fun summary ->
        printfn "Order %d: Amount=%.2f, Taxes=%.2f" summary.order_id summary.total_amount summary.total_taxes
    )
    
    printfn "\nSample monthly summaries (first 5):"
    monthlySummaries
    |> List.take (min 5 monthlySummaries.Length)
    |> List.iter (fun summary ->
        printfn "%04d-%02d: Avg Amount=%.2f, Avg Taxes=%.2f, Orders=%d" 
            summary.year summary.month summary.average_amount summary.average_taxes summary.order_count
    )

// Execute main program
main ()