open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading

let listener = new TcpListener(IPAddress.Loopback, 8090)
listener.Start() // Starts server on port 8090
let mutable clientNumber = 0 // Maintains unique number for connected cleints

let clients = new System.Collections.Generic.List<TcpClient>() // Maintains a list of TCP clients
let cancellationTokenSource = new CancellationTokenSource() // Signals thread cancellation

let checkIfAllIntegers (parts: string array) = // Function to check if all operands are integers
    let mutable allIntegers = true
    for i = 1 to parts.Length - 1 do
        match Int32.TryParse(parts.[i]) with
        | (true, _) -> ()
        | _ -> allIntegers <- false
    allIntegers // Returns boolean value

let performCalculation (operator: string) (parts: string array) = // Function to perofrm calculations on client requests
    let allValuesAreIntegers = checkIfAllIntegers parts
    if parts.Length < 3 then // Checks if number of operands is less than 2 (first value is operation followed by all operands)
        "-2"
    elif parts.Length > 5 then // Checks if number of operands is greater than 4 (first value is operation followed by all operands)
        "-3"
    elif not allValuesAreIntegers then // Checks if all values are integers
        "-4"
    else
        match operator with
        | "+" -> // Performs addition on given operands
            let startIndex = 1
            let mutable result = 0
            for i = startIndex to parts.Length - 1 do
                result <- result + Int32.Parse(parts.[i])
            result.ToString()
        | "-" -> // Performs subtraction on given operands in order (a-b-c)
            let startIndex = 1
            let mutable result = 0
            for i = startIndex to parts.Length - 1 do
                if i = startIndex then
                    result <- Int32.Parse(parts.[i]) // Sets first operand as result to subtract subsequent values
                else
                    result <- result - Int32.Parse(parts.[i]) // Subtracts subsequent values
            result.ToString()
        | "*" -> // Performs multiplications on given operands
            let startIndex = 1
            let mutable result = 1
            for i = startIndex to parts.Length - 1 do
                result <- result * Int32.Parse(parts.[i])
            result.ToString()
        |_ -> "noop" // Indicates invalid operation

let calculate(request: string) = // Converts operation to function
    let parts = request.Split(' ')
    match parts.[0] with
    | "add" -> performCalculation "+" parts
    | "subtract" -> performCalculation "-" parts
    | "multiply" -> performCalculation "*" parts
    | _ -> "noop" // Indiactes invalid operation

let sendTerminationToClients (currentClientNumber: int) = // Sends termiantion request to all clients
    let terminateResponse = -5
    printfn "Responding to client %d with result: %d" currentClientNumber terminateResponse
    for connectedClient in clients do
        try
            let writer = new StreamWriter(connectedClient.GetStream())
            writer.WriteLine(terminateResponse.ToString())
            writer.Flush()
            connectedClient.Close() // Sends code:-5 and closes client connection
        with
        | _ -> ()
    clients.Clear() // Clear the list of clients
    cancellationTokenSource.Cancel() // Cancels running thread
    listener.Server.Close() // Closes Server
    Environment.Exit(0) // Exits program

let rec handleClientAsync (client: TcpClient) = // Handles incoming client request
    async {
        use streamReader = new StreamReader(client.GetStream())
        use streamWriter = new StreamWriter(client.GetStream())

        let currentClientNumber = // Assigns a number to the client when connected
            lock obj (fun () ->
                clientNumber <- clientNumber + 1
                clientNumber
            )
        clients.Add(client) // Adds client to list of connected clients
        while client.Connected do // Loops over incoming client requests for each client until "bye" or "terminate"
            let request = streamReader.ReadLine()
            printfn "Received request from client %d: %s" currentClientNumber request
            if String.Equals(request, "Hello", StringComparison.OrdinalIgnoreCase) then // Handles initial connection
                let response = "Hello!"
                printfn "Responding to client %d with result: %s" currentClientNumber response
                streamWriter.WriteLine(response)
                streamWriter.Flush()
            elif String.Equals(request, "Bye", StringComparison.OrdinalIgnoreCase) then // Handles request to close a particular client connection
                let response = -5
                printfn "Responding to client %d with result: %d" currentClientNumber response
                streamWriter.WriteLine(response)
                streamWriter.Flush()
                client.Close()
            elif String.Equals(request, "terminate", StringComparison.OrdinalIgnoreCase) then // Handles request to close all clients and server connections
                sendTerminationToClients (currentClientNumber)
            else
                try
                    let response = calculate request
                    if String.Equals(response, "noop", StringComparison.OrdinalIgnoreCase) then // Handles response when client inputs and invalid operation
                        let response = -1
                        printfn "Responding to client %d with result: %d" currentClientNumber response
                        streamWriter.WriteLine(response)
                        streamWriter.Flush()
                    else
                        printfn "Responding to client %d with result: %s" currentClientNumber response // Handles response when client inputs a valid operations
                        streamWriter.WriteLine(response)
                        streamWriter.Flush()
                with
                | _ -> client.Close()
    }

async {
    printfn "Server is running and listening on port 8090"

    while true do
        let client = listener.AcceptTcpClient()
        handleClientAsync client |> Async.Start // Starts async function on server
}
|> Async.RunSynchronously
