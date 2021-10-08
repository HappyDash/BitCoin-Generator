#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Security.Cryptography
let k = fsi.CommandLineArgs.[1] |> int
// let k = 4
let serverip = fsi.CommandLineArgs.[2] |> string
// let serverip = "10.20.33.15"
let port = fsi.CommandLineArgs.[3] |>string
// let port = "9002"
let addr = "akka.tcp://ServerSystem@" + serverip + ":" + port + "/user/ServerActor"
// let addr = "akka.tcp://ServerSystem@10.20.33.15:9002/user/ServerActor"
let mutable count=0 //number of workers
let workerscount = System.Environment.ProcessorCount |> int
let configuration =
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 0
                    hostname = 127.0.0.1
                }
            }
        }")
let system = ActorSystem.Create("ClientSystem", configuration)
// Union of Actor messages
type ActorMsg =
    | Worker of int
    | GenerateWorkers of int
    | ResultHandler of string
// Function to generate random string of length n
let ranStr n =
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))
// Logic to compute bitcoins
let ComputeCoin k =
    let mutable answer = ""
    let gatorID = "sankalppandey"
    let mutable counter: int = 0;
    while counter<1 do
        let randomStr: string = ranStr 4
        let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{gatorID}{randomStr}" |> (new SHA256Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
        if stringHash.[..k-1] = String.replicate k "0"
        then
            counter <- counter + 1
            answer <- $"{gatorID}{randomStr}\t{stringHash}\n"
    answer
// Workers actor mailbox function
let FindBitCoin (mailbox:Actor<_>)=
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with
        | Worker(k) ->
                        let answer = ComputeCoin k
                        mailbox.Sender() <! ResultHandler(answer)
        | _ -> printfn "Wrong message recieved by worker"
    }
    loop()
let actoratserver = system.ActorSelection(addr)
let mutable remoteWorkDone = false
// Boss Actor mailbox function
let Boss (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! msg = mailbox.Receive()
        match msg with
        | GenerateWorkers(k) ->
                            let workersList=[for i in 1 .. workerscount do yield(spawn system ("Job" + (string i)) FindBitCoin)]
                            for i in 0 .. (workerscount-1) do
                                workersList.Item(i) <! Worker(k)
        | ResultHandler(answer) ->
                            count <- count+1
                            actoratserver<! answer
                            if count = workerscount then
                                actoratserver<! "Client Done"
                                remoteWorkDone <- true
        | _ -> printfn "Received Wrong message"
        return! loop()
    }
    loop()
let localBossRef = spawn system "localGenerator" Boss
localBossRef <! GenerateWorkers(k)
system.WhenTerminated.Wait()