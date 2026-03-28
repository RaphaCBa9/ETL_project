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

/// Represents an Order from the Order table
type Order = {
    id: int
    client_id: int
    order_date: DateTime
    status: string
    origin: string
}

/// Represents an OrderItem from the OrderItem table
type OrderItem = {
    order_id: int
    product_id: int
    quantity: float
    price: float
    tax: float
}

/// Represents the aggregated result for output
type OrderSummary = {
    order_id: int
    total_amount: float
    total_taxes: float
}

// ============================================================================
// SECTION 2: PURE HELPER FUNCTIONS FOR PARSING
// ============================================================================

/// Parses a string to an integer, returns None if parsing fails
let parseIntOption (str: string) : int option =
    match Int32.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// Parses a string to a float, returns None if parsing fails
let parseFloatOption (str: string) : float option =
    match Double.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// Parses a string to a DateTime, returns None if parsing fails
let parseDateTimeOption (str: string) : DateTime option =
    match DateTime.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

/// Trims whitespace from a string
let trim (str: string) : string =
    str.Trim()

/// Splits a CSV line by comma and trims each field
let splitCsvLine (line: string) : string array =
    line.Split(',') |> Array.map trim

/// Converts a CSV line to an Order record
/// Returns None if any field cannot be parsed correctly
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

/// Converts a CSV line to an OrderItem record
/// Returns None if any field cannot be parsed correctly
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

/// Calculates the revenue for a single OrderItem (quantity * price)
let calculateItemRevenue (item: OrderItem) : float =
    item.quantity * item.price

/// Calculates the tax amount for a single OrderItem (revenue * tax_percentage)
let calculateItemTax (item: OrderItem) : float =
    (calculateItemRevenue item) * item.tax

/// Filters orders by status and origin
/// If status is None, all statuses are accepted
/// If origin is None, all origins are accepted
let filterOrdersByStatusAndOrigin (status: string option) (origin: string option) (order: Order) : bool =
    let statusMatch = match status with
                      | None -> true
                      | Some s -> order.status.ToLower() = s.ToLower()
    let originMatch = match origin with
                      | None -> true
                      | Some o -> order.origin.ToLower() = o.ToLower()
    statusMatch && originMatch

/// Performs an inner join between orders and order items
/// Returns tuples of (Order, OrderItem) for matching order_ids
let innerJoinOrdersAndItems (orders: Order list) (items: OrderItem list) : (Order * OrderItem) list =
    orders
    |> List.collect (fun order ->
        items
        |> List.filter (fun item -> item.order_id = order.id)
        |> List.map (fun item -> (order, item))
    )

/// Groups joined data by order_id and calculates totals
/// Returns a list of OrderSummary records
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

/// Processes the ETL pipeline: filters orders, joins with items, and aggregates
let processETL (orders: Order list) (items: OrderItem list) (statusFilter: string option) (originFilter: string option) : OrderSummary list =
    orders
    |> List.filter (filterOrdersByStatusAndOrigin statusFilter originFilter)
    |> fun filteredOrders -> innerJoinOrdersAndItems filteredOrders items
    |> aggregateOrderTotals
    |> List.sortBy (fun summary -> summary.order_id)

// ============================================================================
// SECTION 4: IMPURE I/O FUNCTIONS
// ============================================================================

/// Reads a CSV file and returns a list of lines (excluding header)
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

/// Converts a list of CSV lines to Order records
let loadOrders (filePath: string) : Order list =
    readCsvFile filePath
    |> List.choose lineToOrder

/// Converts a list of CSV lines to OrderItem records
let loadOrderItems (filePath: string) : OrderItem list =
    readCsvFile filePath
    |> List.choose lineToOrderItem

/// Converts an OrderSummary record to a CSV line
let orderSummaryToCsvLine (summary: OrderSummary) : string =
    sprintf "%d,%.2f,%.2f" summary.order_id summary.total_amount summary.total_taxes

/// Writes OrderSummary records to a CSV file
let writeResultsToCsv (filePath: string) (summaries: OrderSummary list) : unit =
    try
        let header = "order_id,total_amount,total_taxes"
        let lines = header :: (summaries |> List.map orderSummaryToCsvLine)
        File.WriteAllLines(filePath, lines)
        printfn "Results written to: %s" filePath
    with
    | ex ->
        printfn "Error writing to file %s: %s" filePath ex.Message

/// Parses command line arguments
/// Returns (statusFilter option, originFilter option)
let parseArguments (args: string array) : (string option * string option) =
    match args.Length with
    | 0 -> (None, None)
    | 1 -> (Some args.[0], None)
    | _ -> (Some args.[0], Some args.[1])

// ============================================================================
// SECTION 5: MAIN PROGRAM
// ============================================================================

/// Main entry point for the ETL program
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
    
    // Print sample results
    printfn "\nSample results (first 5 orders):"
    results
    |> List.take (min 5 results.Length)
    |> List.iter (fun summary ->
        printfn "Order %d: Amount=%.2f, Taxes=%.2f" summary.order_id summary.total_amount summary.total_taxes
    )

// Execute main program
main ()
