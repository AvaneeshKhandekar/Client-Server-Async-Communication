open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading

let createClient (serverIp: IPAddress) (port: int) = // Starts TCP cleint and connects to server
    let client = new TcpClient()
    client.Connect(serverIp, port)
    client

let terminationFlag = new Object() // Maintains satus for terminate condition
let mutable terminationRequested = false // Updates status based on server broadcast

let receiveAndHandleResponses (client: TcpClient) = // Function to handle server responses
    let reader = new StreamReader(client.GetStream())
    while not terminationRequested do // Continue unless client reveives termination request
        try
            let response = reader.ReadLine()
            match response with // Matches reponse for error codes
            | null -> () // Do nothing
            | "-5" -> // Checks code for terminating client
                printfn "exiit"
                Environment.Exit(0)
                lock terminationFlag (fun () ->
                    terminationRequested <- true )
            | "-4" -> printfn "Received message from server: %s" "one or more of the inputs contain(s) non-number(s)." // Checks for invalid operand errors
            | "-3" -> printfn "Received message from server: %s" "number of inputs is more than four." // Checks for error when operands > 4
            | "-2" -> printfn "Received message from server: %s" "number of inputs is less than two." // Checks for error when operands < 2
            | "-1" -> printfn "Received message from server: %s" "incorrect operation command." // Checks for error when operation is invalid
            | _ -> printfn "Received message from server: %s" response // If no erros continues to display response
        with
        | :? System.IO.IOException as ex ->
            printfn "Error communicating with the server: %s" ex.Message
            exit 1 // Use the exit function to terminate the program

let sendRequestToServer (client: TcpClient) (request: string) = // Function to send requests to server
    try
        let streamWriter = new StreamWriter(client.GetStream())
        printfn "Sending command: %s" request
        streamWriter.WriteLine(request)
        streamWriter.Flush()
    with
    | :? System.IO.IOException as ex ->
        printfn "Error communicating with the server: %s" ex.Message
        exit 1 // Use the exit function to terminate the program

let rec sendRequests(client: TcpClient) = // Sends request by reading console input until termination otherwise stops
    let input = Console.ReadLine()
    if input = "terminate" then
        sendRequestToServer client input
    else
        sendRequestToServer client input
        sendRequests(client) // Continue looping for additional requests

let client = createClient (IPAddress.Parse("127.0.0.1")) 8090 // Creates TCP client
sendRequestToServer client "Hello" // Send first request to server

let responseHandlerThread = new Thread(fun () -> receiveAndHandleResponses client) // Threaing to listen to server responses as well as take client console input
responseHandlerThread.Start() // Start multi threading
sendRequests(client) // Start sending requests
