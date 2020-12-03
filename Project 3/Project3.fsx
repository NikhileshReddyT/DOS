#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open Akka.Actor
open Akka.FSharp
open System
open System.Threading
open System.Text
open System.Collections.Generic
open Akka.Configuration

let mutable numNodes = int(string (fsi.CommandLineArgs.GetValue 1))
let numRequest = int(string (fsi.CommandLineArgs.GetValue 2))
let digitCount = Math.Log(numNodes |> float, 16.0) |> ceil |> int

let mutable nodeId = ""
let mutable len = 0

let mutable actors : Map<String, IActorRef> = Map.empty 
let mutable totalHops = 0
let mutable totalHopCount = 0

let system = ActorSystem.Create("Pastry")
let rand = Random()

let duplicate (text: string) times =
    let sb = new StringBuilder()
    for i in 1..times do
        sb.Append(text) |> ignore
    sb.ToString()

type Msg =
    |Create of String * int
    |Routing of String * String * int
    |Join of String*int
    |EditTable of String[]

let Node (mailBox:Actor<_>) = 
    let mutable nodeId = ""
    let mutable row = 0
    let mutable column = 16
    let mutable routingTable = Array2D.zeroCreate 0 0
    let mutable prefixSize=0
    let mutable currRow=0
    let mutable leafSet = Set.empty
    
    let rec loop() = actor {
            
        let! msg = mailBox.Receive()
        match msg with
            | Create(i,d)->
                nodeId <- i
                row <- d
                routingTable <- Array2D.zeroCreate row column
               
                let mutable counter = 0
                let nodeIdINT = Convert.ToInt32(nodeId, 16)

                let mutable leftNeighbor = nodeIdINT
                let mutable rightNeighbor = nodeIdINT

                while counter < 16 do
                    if counter<8 then 
                        if leftNeighbor = 0 then
                            leftNeighbor <- actors.Count-1
                        leafSet <- leafSet.Add(leftNeighbor.ToString())
                        counter <- counter + 1
                        leftNeighbor <- leftNeighbor - 1
                      
                    else 
                        if rightNeighbor = actors.Count-1 then
                          rightNeighbor <- 0
                        leafSet <- leafSet.Add(rightNeighbor.ToString())
                        counter <- counter + 1
                        rightNeighbor <- rightNeighbor + 1
            
            | Join(key, currIndex) ->
                let mutable i = 0
                let mutable j = 0
                let mutable k = currIndex

                while key.[i] = nodeId.[i] do
                    i<- i+1
                prefixSize <- i
                let mutable routingRow: string[] = Array.zeroCreate 0

                while k<=prefixSize do
                    routingRow <- routingTable |> Seq.cast<'T> |> Seq.toArray
                    routingRow.[Convert.ToInt32(nodeId.[prefixSize].ToString(), 16)] <- nodeId
                    let element = actors.TryFind key
                    match element with
                    | Some x->
                        x<! EditTable(routingRow)
                        ()
                    | None -> printfn "Key not present"

                    k<-k+1

                let routingTableRow = prefixSize
                let routingTableColumn = Convert.ToInt32(key.[prefixSize].ToString(), 16);
                if isNull routingTable.[routingTableRow, routingTableColumn] then
                    routingTable.[routingTableRow, routingTableColumn] <- key
                else
                    let temp = actors.TryFind routingTable.[routingTableRow, routingTableColumn]
                    match temp with
                    | Some x ->
                        x<!Join(key, k)
                    | None ->printfn "Key not present"

            | EditTable(row)->
                routingTable.[currRow, *] <- row
                currRow <- currRow + 1

            | Routing(key, source, hopvalue) ->
                if nodeId = key then
                    totalHops <- totalHops + hopvalue
                    totalHopCount <- totalHopCount + 1

                elif leafSet.Contains(key) then
                    actors.Item(key) <! Routing(key, source, hopvalue+1)

                else
                    let mutable i = 0
                    let mutable j = 0
                    while key.[i] = nodeId.[i] do
                        i<- i+1
                    prefixSize <- i
                    let mutable routingTableRow = prefixSize
                    let mutable routingTableColumn = Convert.ToInt32(key.[prefixSize].ToString(), 16);
                    if isNull routingTable.[routingTableRow, routingTableColumn] then
                        routingTableColumn <- 0
                    actors.Item(routingTable.[routingTableRow, routingTableColumn]) <! Routing(key, source, hopvalue+1)

            | _-> return! loop()

        return! loop()

        }

    loop()


nodeId <- duplicate "0" digitCount
let mutable actor = spawn system nodeId Node
actor <! Create(nodeId, digitCount)

actors<- actors.Add(nodeId, actor)

for i in [1.. numNodes-1] do
    len <- i.ToString("X").Length
    nodeId <-  duplicate "0" (digitCount-len) + i.ToString("X")
    actor <- spawn system nodeId Node
    actor <! Create(nodeId, digitCount)
    actors<- actors.Add(nodeId, actor)
    actors.Item (duplicate "0" digitCount) <! Join(nodeId, 0)
    Thread.Sleep 5

Thread.Sleep 1000

let arr = actors |> Map.toSeq |> Seq.map fst |> Seq.toArray
let mutable destinationId = ""

for k in [1.. numRequest] do
    for sourceId in arr do
        destinationId <- sourceId
        while destinationId = sourceId do
            destinationId <-  arr.[rand.Next arr.Length]
        actors.Item sourceId <! Routing(destinationId, sourceId, 0)
        Thread.Sleep 5
        
Thread.Sleep 1000

printfn "Average Hop size = %A" ((totalHops |> double) / (totalHopCount |> double))