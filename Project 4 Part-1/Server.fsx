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

let mutable nodeMap : Map<String, IActorRef> = Map.empty

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
                    port = 8777
                    hostname = localhost
                }
            }
        }")

type Subscribe = {Usrname : string; SubID : string}
type Tweet = {Usrname : string; Msg : string}
type Retweet = {Usrname : string; Msg : string}
type Register = {ID : int; Usrname : string; Pwd : string}
type Mention = {At : string; Usrname : string}
type HashTag = {Tag : string; Usrname : string}

let system = System.create "RemoteFSharp" configuration

            
type MsgSubscribe = SubscribeMsg of  string * string * IActorRef
type MsgTweet = TweetMessage of  string * string * IActorRef
type MsgRetweet = RetweetMessage of  string * string * IActorRef
type MessageRegister = RegistrationMsg of  int  * string  * string * IActorRef
type MsgMention = MentionMsg of  string * IActorRef
type MsgHashTag = HashtagMsg of  string * IActorRef

let ActorAT (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | MentionMsg(mentionedUser,sender) -> 
                        if (AtMap.ContainsKey mentionedUser) then
                            let atList = AtMap.Item mentionedUser
                            let mutable response = ""
                            for i in atList do
                                response <- response + "***" + i
                            sender <! response
                        else
                            sender <! "no such mentions"
                | _ ->  failwith "unknown message"
                return! loop()
            } 
        loop()

let Login (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | RegistrationMsg(ID,username,pwd,sender) -> 
                    if(ID = 0) then
                        sender <! (userRegistration username pwd)
                    else if(ID = 1) then
                        let mutable response = "login failed"
                        // let mutable b = true
                        if(usrMap.ContainsKey username) then
                            let password = usrMap.Item username
                            if (password = pwd) then
                                UserActivation  username true
                                response <- ""
                                let pf = feed username
                                for i in pf do
                                    response <- response + "***" + i
                            sender <! response
                    else
                        UserActivation username false
                        sender <! "successfully logged out"
                | _ ->  failwith "unknown message"
                return! loop()
            } 
        loop()

let ActorSubscribe (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | SubscribeMsg(uid,pid,sender) -> 
                        //printfn "A"
                        addSubscriber uid pid |> ignore
                        sender <! "success"
                | _ ->  failwith "unknown message"
                return! loop()
            } 
        loop()

let ActorHashtag (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | HashtagMsg(hashtag,sender) -> 
                        if (HTmap.ContainsKey hashtag) then
                            let HTtweets = HTmap.Item hashtag
                            let mutable response = ""
                            for i in HTtweets do
                                response <- response + "***" + i
                            sender <! response
                        else
                            sender <! "no such hashtags"
                | _ ->  failwith "unknown message"
                return! loop()
            } 
        loop()

let ActorRetweets (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | RetweetMessage(uid,Msg,sender) -> 
                        let subscribersList = SubscribersMap.Item uid
                        for i in subscribersList do
                            setRetweet Msg uid i
                        sender <! "success"
                | _ ->  failwith "unknown message"
                return! loop()
            } 
        loop()

let ActorTweet (mailbox: Actor<_>)=
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                match message with
                | TweetMessage(uid,Msg,sender) -> 
                        let hts = hashtagQuery Msg
                        setHT hts Msg
                        let ATs = mentionQuery Msg
                        setMentions ATs Msg uid
                        let subscribersList = SubscribersMap.Item uid
                        for i in subscribersList do
                            setTweet Msg uid i
                        sender <! "success"
                | _ ->  failwith "unknown message"
                return! loop()
            }
        loop()

let HTActor =  spawn system "ActorHashtag" ActorHashtag
let actorTweets = spawn system "ActorTweet" ActorTweet
let actorLogin = spawn system "Login" Login
let AtActor =  spawn system "ActorAT" ActorAT
let SubscribeActor = spawn system "ActorSubscribe" ActorSubscribe
let RetweetActor = spawn system "ActorRetweets" ActorRetweets

let echoServer = 
    spawn system "EchoServer"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! message = mailbox.Receive()
                let sender = mailbox.Sender()
                match box message with
                | :? Subscribe as msg-> 
                    if (userLoginCheck msg.Usrname) then
                        SubscribeActor <! SubscribeMsg(msg.Usrname,msg.SubID,sender)
                    else
                        sender <! "user not logged in"
                | :? Tweet as msg-> 
                    if (userLoginCheck msg.Usrname) then
                        actorTweets <! TweetMessage(msg.Usrname,msg.Msg,sender)
                    else
                        sender <! "user not logged in"
                | :? Retweet as msg-> 
                    if (userLoginCheck msg.Usrname) then
                        RetweetActor <! RetweetMessage(msg.Usrname,msg.Msg,sender)
                    else
                        sender <! "user not logged in"
                | :? Register as msg-> 
                    actorLogin <! RegistrationMsg(msg.ID,msg.Usrname,msg.Pwd,sender)
                | :? Mention as msg-> 
                    if (userLoginCheck msg.Usrname) then
                        AtActor <! MentionMsg(msg.At,sender)
                    else
                        sender <! "user not logged in"
                | :? HashTag as msg-> 
                    if (userLoginCheck msg.Usrname) then
                        HTActor <! HashtagMsg(msg.Tag,sender)
                    else
                        sender <! "user not logged in"
                | _ ->  failwith "Unknown message"
                return! loop()
            }
        loop()

System.Console.ReadLine() |> ignore
system.Terminate() |> ignore