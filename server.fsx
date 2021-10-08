
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Security.Cryptography

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 9002
                    hostname = 10.20.33.15
                }
            }
        }")

// Union of Actor mailbox function
type ActorMessage =
    | WorkerMessage of int
    | GeneratorMessage of int
    | ServerMessage of int
    | FinishMessage of string
    | DefaultMessage of string
    | CommonMessage of string

let k = fsi.CommandLineArgs.[1] |> int
let workersReq = System.Environment.ProcessorCount |> int
let system = ActorSystem.Create("ServerSystem", configuration)
let mutable serverWorkDone: bool = false
let mutable clientWorkDone: bool = false
let mutable count=0

#time "on"

// Function to generate random strings
let ranStr n =
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))

// Logic to compute bitcoins
let ComputeCoin k =
    let mutable answer = ""
    let gatorID = "sankalppandey"
    let mutable counter: int = 0
    while counter<1 do
        let randomStr: string = ranStr 4
        let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{gatorID}{randomStr}" |> (new SHA256Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
        if stringHash.[..k-1] = String.replicate k "0"
        then
            counter <- counter + 1
            answer <- $"{gatorID}{randomStr}\t{stringHash}\n"
    answer

// workers actor mailbox function
let workerActorHandler (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | WorkerMessage(k) -> 
                        let ans = ComputeCoin k
                        mailbox.Sender() <! FinishMessage(ans)
        | _ -> printfn "Error messaged received"
    }
    loop()

// Box actor mailbox function
let BossActorHandler (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with 
        | GeneratorMessage(k) ->   
                                let workersList=[for a in 1 .. workersReq do yield(spawn system ("Worker" + (string a)) workerActorHandler)]
                                printfn "Generating workers"
                                for i in 0 .. (workersReq-1) do
                                    workersList.Item(i) <! WorkerMessage(k)
                                                                
        | FinishMessage(answer) -> 
                            printfn "%s" answer
                            count <- count+1
                            if count = 8 then
                                serverWorkDone <- true
                            if(clientWorkDone = true && serverWorkDone = true)
                            then
                                system.Terminate() |> ignore
        | CommonMessage(answer) -> printfn "%s" answer
        | _ -> printfn "Dispatcher Received Wrong message"
        return! loop()
    }
    loop()

let BossRef = spawn system "generator" BossActorHandler

// Actor for starting boss actor and printing results from server and client
let echoServer = 
    spawn system "ServerActor"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? string ->
                    if(message = "start")
                    then
                        BossRef <! GeneratorMessage(k)
                    else if(message = "Client Done") 
                    then
                        clientWorkDone <- true
                    else
                        printfn "%s" message

                    if(clientWorkDone = true && serverWorkDone = true)
                    then
                        system.Terminate() |> ignore
                    return! loop()
                | _ ->  failwith "Unknown message"
            }
        loop()

echoServer <! "start" // starting point of server
system.WhenTerminated.Wait()