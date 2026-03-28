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
/// <remarks>
/// This record contains the core information about a customer order,
/// including its unique identifier, the associated client, order date,
/// current status, and the channel through which it was placed.
/// </remarks>
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
/// <remarks>
/// This record contains details about a specific item within an order,
/// including the product reference, quantity ordered, price paid at purchase time,
/// and the applicable tax percentage.
/// </remarks>
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
/// <remarks>
/// This record is the primary output of the ETL transformation,
/// containing the order identifier and its calculated financial totals.
/// </remarks>
type OrderSummary = {
    order_id: int
    total_amount: float
    total_taxes: float
}

/// <summary>
/// Represents monthly and yearly aggregated statistics for orders.
/// </summary>
/// <remarks>
/// This record contains aggregated financial metrics grouped by month and year,
/// useful for trend analysis and reporting.
/// </remarks>
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
/// <param name="str">The string to parse.</param>
/// <returns>Some value if parsing succeeds, None otherwise.</returns>
/// <remarks>
/// This function uses the TryParse pattern to safely convert strings to integers
/// without throwing exceptions, making it suitable for untrusted input.
/// </remarks>
let parseIntOption (str: string) : int option =
    match Int32.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Parses a string to a floating-point number.
/// </summary>
/// <param name="str">The string to parse.</param>
/// <returns>Some value if parsing succeeds, None otherwise.</returns>
/// <remarks>
/// This function handles both integer and decimal formats, using the TryParse
/// pattern to ensure safe conversion without exceptions.
/// </remarks>
let parseFloatOption (str: string) : float option =
    match Double.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Parses a string to a DateTime value.
/// </summary>
/// <param name="str">The string to parse.</param>
/// <returns>Some value if parsing succeeds, None otherwise.</returns>
/// <remarks>
/// This function accepts multiple DateTime formats and uses the TryParse pattern
/// for safe conversion. It is essential for extracting temporal information from CSV data.
/// </remarks>
let parseDateTimeOption (str: string) : DateTime option =
    match DateTime.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// <summary>
/// Trims leading and trailing whitespace from a string.
/// </summary>
/// <param name="str">The string to trim.</param>
/// <returns>The trimmed string.</returns>
/// <remarks>
/// This is a utility function used to clean CSV fields that may contain
/// extraneous whitespace.
/// </remarks>
let trim (str: string) : string =
    str.Trim()

/// <summary>
/// Splits a CSV line by comma and trims each resulting field.
/// </summary>
/// <param name="line">A single line from a CSV file.</param>
/// <returns>An array of trimmed field values.</returns>
/// <remarks>
/// This function assumes comma-separated values. Each field is trimmed to remove
/// leading and trailing whitespace that may have been introduced during splitting.
/// </remarks>
let splitCsvLine (line: string) : string array =
    line.Split(',') |> Array.map trim

/// <summary>
/// Converts a CSV line to an Order record.
/// </summary>
/// <param name="line">A single line from the order CSV file.</param>
/// <returns>Some Order if all fields parse correctly, None otherwise.</returns>
/// <remarks>
/// This function validates that all required fields are present and can be parsed.
/// If any field fails to parse, the entire line is rejected, ensuring data integrity.
/// Expected CSV format: id,client_id,order_date,status,origin
/// </remarks>
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
/// <param name="line">A single line from the order item CSV file.</param>
/// <returns>Some OrderItem if all fields parse correctly, None otherwise.</returns>
/// <remarks>
/// This function validates that all required fields are present and can be parsed.
/// If any field fails to parse, the entire line is rejected.
/// Expected CSV format: order_id,product_id,quantity,price,tax
/// </remarks>
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
/// <param name="item">The OrderItem to calculate revenue for.</param>
/// <returns>The revenue as quantity multiplied by price.</returns>
/// <remarks>
/// Revenue is a fundamental metric calculated as the product of quantity and unit price.
/// This pure function has no side effects and is deterministic.
/// </remarks>
let calculateItemRevenue (item: OrderItem) : float =
    item.quantity * item.price

/// <summary>
/// Calculates the tax amount for a single OrderItem.
/// </summary>
/// <param name="item">The OrderItem to calculate tax for.</param>
/// <returns>The tax amount as revenue multiplied by the tax percentage.</returns>
/// <remarks>
/// Tax is calculated as a percentage of the item's revenue.
/// This function depends on calculateItemRevenue, demonstrating function composition.
/// </remarks>
let calculateItemTax (item: OrderItem) : float =
    (calculateItemRevenue item) * item.tax

/// <summary>
/// Filters orders by status and origin criteria.
/// </summary>
/// <param name="status">Optional status filter. None means accept all statuses.</param>
/// <param name="origin">Optional origin filter. None means accept all origins.</param>
/// <param name="order">The Order to evaluate.</param>
/// <returns>True if the order matches all provided filters, false otherwise.</returns>
/// <remarks>
/// This function implements optional filtering logic. If a filter parameter is None,
/// that dimension is not filtered. Comparisons are case-insensitive.
/// </remarks>
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
/// </summary>
/// <param name="orders">The list of orders to join.</param>
/// <param name="items">The list of order items to join.</param>
/// <returns>A list of tuples containing (Order, OrderItem) pairs for matching order_ids.</returns>
/// <remarks>
/// This function implements a functional inner join by collecting all items
/// that correspond to each order. Orders with no items are excluded.
/// </remarks>
let innerJoinOrdersAndItems (orders: Order list) (items: OrderItem list) : (Order * OrderItem) list =
    orders
    |> List.collect (fun order ->
        items
        |> List.filter (fun item -> item.order_id = order.id)
        |> List.map (fun item -> (order, item))
    )

/// <summary>
/// Groups joined data by order_id and calculates aggregated totals.
/// </summary>
/// <param name="joinedData">The list of (Order, OrderItem) tuples to aggregate.</param>
/// <returns>A list of OrderSummary records containing calculated totals.</returns>
/// <remarks>
/// This function uses List.groupBy to partition data by order ID, then applies
/// fold operations to sum revenues and taxes for each order.
/// </remarks>
let aggregateOrderTotals (joinedData: (Order * OrderItem) list) : OrderSummary list =
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
/// </summary>
/// <param name="orders">The list of orders to process.</param>
/// <param name="items">The list of order items to process.</param>
/// <param name="statusFilter">Optional status filter.</param>
/// <param name="originFilter">Optional origin filter.</param>
/// <returns>A sorted list of OrderSummary records.</returns>
/// <remarks>
/// This function orchestrates the entire transformation pipeline:
/// 1. Filters orders based on provided criteria
/// 2. Performs inner join with items
/// 3. Aggregates totals by order
/// 4. Sorts results by order ID
/// </remarks>
let processETL (orders: Order list) (items: OrderItem list) (statusFilter: string option) (originFilter: string option) : OrderSummary list =
    orders
    |> List.filter (filterOrdersByStatusAndOrigin statusFilter originFilter)
    |> fun filteredOrders -> innerJoinOrdersAndItems filteredOrders items
    |> aggregateOrderTotals
    |> List.sortBy (fun summary -> summary.order_id)

/// <summary>
/// Calculates monthly and yearly aggregated statistics from order summaries.
/// </summary>
/// <param name="orders">The list of orders to aggregate.</param>
/// <param name="summaries">The list of OrderSummary records to process.</param>
/// <returns>A list of MonthlySummary records grouped by year and month.</returns>
/// <remarks>
/// This function groups order summaries by their month and year, calculating
/// average amounts and taxes for each period. It requires the original orders
/// to extract date information.
/// </remarks>
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
// SECTION 4: IMPURE I/O FUNCTIONS
// ============================================================================

/// <summary>
/// Reads a CSV file and returns a list of lines, excluding the header.
/// </summary>
/// <param name="filePath">The path to the CSV file.</param>
/// <returns>A list of CSV lines without the header.</returns>
/// <remarks>
/// This function handles file I/O errors gracefully, printing error messages
/// and returning an empty list if the file cannot be read.
/// </remarks>
let readCsvFile (filePath: string) : string list =
    try
        File.ReadAllLines(filePath)
        |> Array.toList
        |> function
            | [] -> []
            | _ :: tail -> tail  // Skip header line
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
/// <param name="filePath">The path to the order CSV file.</param>
/// <returns>A list of successfully parsed Order records.</returns>
/// <remarks>
/// This function reads the CSV file and applies the lineToOrder parser
/// to each line, collecting only successfully parsed records.
/// </remarks>
let loadOrders (filePath: string) : Order list =
    readCsvFile filePath
    |> List.choose lineToOrder

/// <summary>
/// Loads OrderItem records from a CSV file.
/// </summary>
/// <param name="filePath">The path to the order item CSV file.</param>
/// <returns>A list of successfully parsed OrderItem records.</returns>
/// <remarks>
/// This function reads the CSV file and applies the lineToOrderItem parser
/// to each line, collecting only successfully parsed records.
/// </remarks>
let loadOrderItems (filePath: string) : OrderItem list =
    readCsvFile filePath
    |> List.choose lineToOrderItem

/// <summary>
/// Converts an OrderSummary record to a CSV line.
/// </summary>
/// <param name="summary">The OrderSummary to convert.</param>
/// <returns>A formatted CSV line string.</returns>
/// <remarks>
/// This function formats the summary with two decimal places for monetary values,
/// ensuring consistent output format.
/// </remarks>
let orderSummaryToCsvLine (summary: OrderSummary) : string =
    sprintf "%d,%.2f,%.2f" summary.order_id summary.total_amount summary.total_taxes

/// <summary>
/// Converts a MonthlySummary record to a CSV line.
/// </summary>
/// <param name="summary">The MonthlySummary to convert.</param>
/// <returns>A formatted CSV line string.</returns>
/// <remarks>
/// This function formats the monthly summary with two decimal places for monetary values
/// and proper date formatting.
/// </remarks>
let monthlySummaryToCsvLine (summary: MonthlySummary) : string =
    sprintf "%04d-%02d,%.2f,%.2f,%d" summary.year summary.month summary.average_amount summary.average_taxes summary.order_count

/// <summary>
/// Writes OrderSummary records to a CSV file.
/// </summary>
/// <param name="filePath">The path where the CSV file should be written.</param>
/// <param name="summaries">The list of OrderSummary records to write.</param>
/// <remarks>
/// This function writes the summaries with a proper CSV header and handles
/// I/O errors gracefully.
/// </remarks>
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
/// <param name="filePath">The path where the CSV file should be written.</param>
/// <param name="summaries">The list of MonthlySummary records to write.</param>
/// <remarks>
/// This function writes monthly aggregated statistics with a proper CSV header
/// and handles I/O errors gracefully.
/// </remarks>
let writeMonthlySummariesToCsv (filePath: string) (summaries: MonthlySummary list) : unit =
    try
        let header = "year-month,average_amount,average_taxes,order_count"
        let lines = header :: (summaries |> List.map monthlySummaryToCsvLine)
        File.WriteAllLines(filePath, lines)
        printfn "Monthly summaries written to: %s" filePath
    with
    | ex ->
        printfn "Error writing to file %s: %s" filePath ex.Message

/// <summary>
/// Parses command line arguments into optional filter parameters.
/// </summary>
/// <param name="args">The command line arguments array.</param>
/// <returns>A tuple of (statusFilter option, originFilter option).</returns>
/// <remarks>
/// This function interprets command line arguments as filters:
/// - No arguments: (None, None) - process all orders
/// - One argument: (Some status, None) - filter by status only
/// - Two or more arguments: (Some status, Some origin) - filter by both
/// </remarks>
let parseArguments (args: string array) : (string option * string option) =
    match args.Length with
    | 0 -> (None, None)
    | 1 -> (Some args.[0], None)
    | _ -> (Some args.[0], Some args.[1])

// ============================================================================
// SECTION 5: MAIN PROGRAM
// ============================================================================

/// <summary>
/// Main entry point for the ETL program.
/// </summary>
/// <remarks>
/// This function orchestrates the entire ETL process:
/// 1. Parses command line arguments
/// 2. Loads data from CSV files
/// 3. Processes the ETL pipeline
/// 4. Writes results to output files
/// 5. Displays sample results to the console
/// </remarks>
let main () =
    // Parse command line arguments
    let (statusFilter, originFilter) = parseArguments fsi.CommandLineArgs.[1..]
    
    // Print filter information
    match (statusFilter, originFilter) with
    | (None, None) -> printfn "Processing all orders (no filters applied)"
    | (Some status, None) -> printfn "Processing orders with status: %s" status
    | (None, Some origin) -> printfn "Processing orders with origin: %s" origin
    | (Some status, Some origin) -> printfn "Processing orders with status: %s and origin: %s" status origin
    
    // Load data from CSV files
    printfn "Loading orders from order.csv..."
    let orders = loadOrders "order.csv"
    printfn "Loaded %d orders" orders.Length
    
    printfn "Loading order items from order_item.csv..."
    let items = loadOrderItems "order_item.csv"
    printfn "Loaded %d order items" items.Length
    
    // Process ETL pipeline
    printfn "Processing ETL pipeline..."
    let results = processETL orders items statusFilter originFilter
    printfn "Generated %d order summaries" results.Length
    
    // Write results to output CSV
    writeResultsToCsv "output.csv" results
    
    // Calculate and write monthly summaries
    printfn "Calculating monthly summaries..."
    let monthlySummaries = calculateMonthlySummaries orders results
    writeMonthlySummariesToCsv "monthly_summary.csv" monthlySummaries
    printfn "Generated %d monthly summaries" monthlySummaries.Length
    
    // Print sample results
    printfn "\nSample results (first 5 orders):"
    results
    |> List.take (min 5 results.Length)
    |> List.iter (fun summary ->
        printfn "Order %d: Amount=%.2f, Taxes=%.2f" summary.order_id summary.total_amount summary.total_taxes
    )
    
    // Print sample monthly summaries
    printfn "\nSample monthly summaries (first 5):"
    monthlySummaries
    |> List.take (min 5 monthlySummaries.Length)
    |> List.iter (fun summary ->
        printfn "%04d-%02d: Avg Amount=%.2f, Avg Taxes=%.2f, Orders=%d" 
            summary.year summary.month summary.average_amount summary.average_taxes summary.order_count
    )

// Execute main program
main ()