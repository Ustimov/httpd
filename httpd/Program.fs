open System.IO
open System.Net.Sockets

let mutable ipAddress = "127.0.0.1"
let mutable port = 80
let mutable documentRoot = ""

type Socket with
    member socket.AsyncAccept() = Async.FromBeginEnd(socket.BeginAccept, socket.EndAccept)
    member socket.AsyncReceive(buffer:byte[]) = 
        let beginReceive(buffer, offset, count, callback, state) = 
            socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state)
        Async.FromBeginEnd(buffer, 0, buffer.Length, beginReceive, socket.EndReceive)
    member socket.AsyncSend(buffer:byte[]) = 
        let beginSend (buffer, offset, count, callback, state) =
            socket.BeginSend(buffer, offset, count, SocketFlags.None, callback, state)
        Async.FromBeginEnd(buffer, 0, buffer.Length, beginSend, socket.EndSend)

let createContent contentLength contentType content = 
    let headers = sprintf "Content-Length: %d\r\nContent-Type: %s\r\n\r\n" contentLength contentType
    [| System.Text.Encoding.UTF8.GetBytes(headers); content |] |> Array.concat

let createResponse status = 
    let headers = sprintf "HTTP/1.1 %s\r\nDate: %A\r\nServer: F#\r\nConnection: close\r\n" status System.DateTime.Now
    System.Text.Encoding.UTF8.GetBytes(headers)

let processRequest (request: string) = 
    async {
        try
            let methodPathProtocol = request.Trim().Split(' ')   
            if methodPathProtocol.Length <> 3 || methodPathProtocol.[0].Length = 0
                || methodPathProtocol.[1].Length = 0 ||  methodPathProtocol.[2].Length = 0 then failwith "400 Bad request"
            methodPathProtocol.[1] <- System.Net.WebUtility.UrlDecode(methodPathProtocol.[1]).Split('?').[0]
            let path = 
                match methodPathProtocol.[1] with
                | _ when methodPathProtocol.[1].Contains("/../") -> failwith "400 Bad request"
                | _ when methodPathProtocol.[1].EndsWith("/") -> 
                    Path.Combine(documentRoot, methodPathProtocol.[1].Substring(1), "index.html")
                | _ -> Path.Combine(documentRoot, methodPathProtocol.[1].Substring(1))
            let mime = System.Web.MimeMapping.GetMimeMapping(path)
            let! responseContent = 
                match methodPathProtocol.[0] with
                | _ when File.Exists(path) = false && path.EndsWith("index.html") -> failwith "403 Forbidden"
                | _ when File.Exists(path) = false -> failwith "404 Not found"
                | "HEAD" -> async { return createContent ((new FileInfo(path)).Length) mime ""B }
                | "GET" -> async { use file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                                   let! content = file.AsyncRead(int ((new FileInfo(path)).Length))               
                                   return createContent ((new FileInfo(path)).Length) mime content }
                | _ -> failwith "405 Method not allowed"
            let status = "200 OK"
            return [| createResponse status; responseContent |] |> Array.concat, status
        with
        | Failure status -> return createResponse status, status }

let handler (connection: Socket) = 
    async {
        let buffer : byte[] = Array.zeroCreate 1024 
        let! bytesReceived = connection.AsyncReceive(buffer)
        let request = System.Text.Encoding.UTF8.GetString(buffer)
        if request.Contains("\r\n") then
            let header = request.Substring(0, request.IndexOf("\r\n"))
            printf "[%A] %s (%d, " System.DateTime.Now header bytesReceived
            let! response, status = processRequest header
            let! bytesSent = connection.AsyncSend(response)
            printfn "%d) %s" bytesSent status
            connection.Shutdown(SocketShutdown.Both)
            connection.Close() }

let server =  
    let rec parse args =
        match args with
        | h::t when h = "-r" && Path.IsPathRooted(t.Head) -> documentRoot <- t.Head
        | h::t when h = "-p" -> port <- int t.Head
        | h::t when h = "-a" -> ipAddress <- t.Head
        | _ -> ()
        if args <> [] then parse args.Tail else ()
    parse (Array.toList(System.Environment.GetCommandLineArgs()))
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port))
    socket.Listen(100)
    printf "Server started at %s:%d" ipAddress port
    if documentRoot = "" then printfn " with current directory as DOCUMENT_ROOT"
        else printfn " with %s as DOCUMENT_ROOT" documentRoot
    let rec listen () =
        async { let! connection = socket.AsyncAccept()
                Async.Start(handler connection)
                do! listen () }
    listen ()

Async.RunSynchronously(server)