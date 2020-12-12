#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 
#load @"./Operations.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Operations


let args = fsi.CommandLineArgs |> Array.tail
let totalClients = args.[0] |> int

type Register = {ID : int; Usrname : string; Pwd : string}
type Tweet = {Usrname : string; Msg : string}
type Subscribe = {Usrname : string; SubID : string}
type HashTag = {Tag : string; Usrname : string}
type Mention = {At : string; Usrname : string}
type Retweet = {Usrname : string; Msg : string}

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
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
                    port = 8123
                    hostname = localhost
                }
            }
        }")

let system = ActorSystem.Create("RemoteFSharp", configuration)
let Client = system.ActorSelection("akka.tcp://RemoteFSharp@localhost:8777/user/EchoServer")

let mutable numClients = 0

type NodeMessage =
    |Begin of (int)
    |Bn
    |Finish of (int)

let hashtags = [|"#ht1" ; "#ht2" ; "#ht3" ; "#ht4" ; "#ht5"|]

let Node (auditor : IActorRef) (x : int) (mailBox:Actor<_>) = 
        let rec loop() = actor {
            
            let! message = mailBox.Receive()
                    
            match message with 
                | Begin(x) ->
                        let mutable res = ""
                        let Usrname = ((new System.Random()).Next(totalClients)) + 1
                        // printfn "%d  --" Usrname
                        let rand = ((new System.Random()).Next(6))
                        if ( rand = 0 ) then
                            res <- Async.RunSynchronously ((Client <? {Tweet.Usrname = "user" + (string Usrname); Tweet.Msg =  "tweet" + (string Usrname) (*+ (string i)*) + " #game @user1"}), 3000)
                        else if ( rand = 1 ) then
                            res <- Async.RunSynchronously ((Client <? {Register.ID = 2; Register.Usrname = "user"+(string Usrname); Register.Pwd = "password"+(string Usrname)}), 1000)
                        else if ( rand = 2 ) then
                            res <- Async.RunSynchronously ((Client <? {Register.ID = 1; Register.Usrname = "user"+(string Usrname); Register.Pwd = "password"+(string Usrname)}), 1000)
                        else if ( rand = 3 ) then
                            res <- Async.RunSynchronously ((Client <? {HashTag.Tag = hashtags.[((new System.Random()).Next(5))]; HashTag.Usrname = "user" + (string Usrname)}), 1000)
                        else if ( rand = 4 ) then
                            res <- Async.RunSynchronously ((Client <? {Mention.At = "@user"+(string Usrname); Mention.Usrname = "user" + (string Usrname)}), 1000)
                        else if ( rand = 5 ) then
                            res <- Async.RunSynchronously ((Client <? {Retweet.Usrname = "user2"; Retweet.Msg =  "Retweetedby:usery|author:userx = tweetx #game @user1"}), 1000)
                        auditor <! Finish(Usrname)
                | _-> return! loop()
            return! loop()
        }
        loop()

let Master (mailbox:Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with 
        | Bn ->
            let node = Node mailbox.Context.Self (0) |> spawn system ("Node" + string(id))
            for i in 1 .. totalClients do
                node <! Begin(i)
        | Finish(x) ->
            numClients <- numClients + 1
            if (numClients = totalClients) then
                mailbox.Context.System.Terminate() |> ignore
        | _ -> ()
        return! loop()
    }
    loop()

let mutable resp = ""

let mutable stopWatch = System.Diagnostics.Stopwatch.StartNew()
for i in 1..totalClients do
    // printfn "%d" i
    resp <- Async.RunSynchronously ((Client <? {Register.ID = 0; Register.Usrname = "user"+(string i); Register.Pwd = "password"+(string i)}), 1000)

stopWatch.Stop()
let regTime = stopWatch.Elapsed.TotalMilliseconds

stopWatch <- System.Diagnostics.Stopwatch.StartNew()
let mutable temp = 1
for i in 1..totalClients do
    for j in 1..temp..totalClients do
        // printfn "%d == %d" i j
        if not (i = j) then
            let a = (if (j = 0) then i/2 else j)
            resp <- Async.RunSynchronously ((Client <? {Subscribe.Usrname = "user"+(string a); Subscribe.SubID =  "user"+(string i)}), 1000)
    temp <- (i+1)*(i+1)

stopWatch.Stop()
let subTime = stopWatch.Elapsed.TotalMilliseconds

stopWatch <- System.Diagnostics.Stopwatch.StartNew()
for i in 1..totalClients do
            let rand = ((new System.Random()).Next(5))
            resp <- Async.RunSynchronously ((Client <? {Tweet.Usrname = "user"+(string i); Tweet.Msg =  "tweet" + (string i) + "0" (*+ (string k)*) + " " + hashtags.[rand] + " @user" + (string (i/2))}), 1000)

stopWatch.Stop()
let sendTime = stopWatch.Elapsed.TotalMilliseconds

stopWatch <- System.Diagnostics.Stopwatch.StartNew()
let master = spawn system "master" Master
master <? Bn
system.WhenTerminated.Wait()
stopWatch.Stop()
let perfTime = stopWatch.Elapsed.TotalMilliseconds

printfn "\nRegistration Time %d users : %f" totalClients regTime
printfn "Zipf subscribe Time %d users : %f" totalClients subTime
printfn "Tweeting time %d users : %f" totalClients sendTime
printfn "Total Time %d users: %f" totalClients perfTime

system.Terminate() |> ignore