#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
// #load "Bootstrap.fsx"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let system = System.create "MySystem" (Configuration.defaultConfig())

let mutable Actors = 0

let upperLimit = fsi.CommandLineArgs.[1]|> int
let numOfElements = fsi.CommandLineArgs.[2]|> int

let checkPerfectSquare (num: uint64) =
    let temp = round (sqrt (double num)) |> uint64
    temp * temp = num

let Worker (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? string as msg -> 
            let mutable sum = 0 |> uint64
            let info = msg.Split '|'
            let startRange = info.[0] |> uint64
            let endRange = info.[1] |> uint64
            let x = numOfElements |> uint64
            let mutable i = startRange 

            while i <= endRange do
                sum <- (uint64 0)
                let mutable j = 0 |> uint64
                for j in i .. i + x - (uint64 1) do
                    sum <- sum + (j*j)
                if checkPerfectSquare sum then
                    printfn "%d \n" i
                i <- i + (uint64 1)

        sender <! "done"
        return! loop()
    }
    loop()

let mutable actorCount = 0
let mutable checkValue = true

let Boss (mailbox: Actor<_>) =
    let rec loop() = actor {   
        let! message = mailbox.Receive()
        match box message with
        | :? string as msg ->  
            if msg = "done" then
                actorCount <- actorCount - 1
                if(actorCount = 0) then
                   checkValue <- false
                   
            else
                let numOfActors = if upperLimit >= 100 then 16 else 2
                actorCount <- numOfActors

                let listOfWorkers = 
                    [1 .. numOfActors]
                    |> List.map(fun id ->   spawn system (string id) Worker)

                let mutable previous = 0
                let split = ceil (float upperLimit / float numOfActors)
                for j in 1 .. numOfActors do
                    let start = previous + 1
                    let mutable finish = start + (int split)
                    if upperLimit < finish then finish <- upperLimit
                    previous <- finish
                    j-1 |> List.nth listOfWorkers <! sprintf "%d|%d" start finish

        return! loop()
    }  
    loop()

let b = spawn system "myMon" Boss
b <! sprintf "Initiate Program"

while(checkValue) do
  ignore