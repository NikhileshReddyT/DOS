#if INTERACTIVE
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#endif

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic

type Gossip =
    |Initialize of IActorRef[]
    |BeginGossip of String
    |GossipResult of String
    |BeginPushSum of Double
    |EvaluatePushSum of Double * Double * Double
    |PushSumResult of Double * Double
    |StartTimer of int
    |NumParticipants of int

type Handler() =
    inherit Actor()
    let mutable rmessages = 0
    let mutable numParticipants = 0
    let stopwatch = System.Diagnostics.Stopwatch()

    override x.OnReceive(msgReceived) = 
        match msgReceived :?>Gossip with 
        | GossipResult message ->
            rmessages <- rmessages + 1
            if rmessages = numParticipants then
                stopwatch.Stop()
                printfn "Convergence Time: %f ms" stopwatch.Elapsed.TotalMilliseconds
                Environment.Exit(0)  
           

        | PushSumResult (sum,weight) ->
            stopwatch.Stop()
            printfn "Convergence Time: %f ms" stopwatch.Elapsed.TotalMilliseconds
            Environment.Exit(0)

        | StartTimer x ->
            stopwatch.Start()

        | NumParticipants n ->
            numParticipants <- n
        | _->()
 
type Node(handler: IActorRef, nodeID: int) =
    inherit Actor()
    let mutable heardMsgs = 0 
    let mutable  neighbours:IActorRef[]=[||]

    //used for push sum
    let mutable sum = nodeID |> float
    let mutable weight = 1.0
    let mutable temp = 1

    
 
    override x.OnReceive(number)=
         match number :?>Gossip with 
         | Initialize i ->
                neighbours <- i

         | BeginGossip message ->
                heardMsgs<- heardMsgs+1
                if(heardMsgs=10) then 
                      handler <! GossipResult(message)
                if(heardMsgs <=10) then
                        for i in 1..10 do
                            neighbours.[((new System.Random()).Next(0,neighbours.Length))] <! BeginGossip(message)

         | BeginPushSum n -> 
                        sum<- sum/2.0
                        weight <-weight/2.0
                        neighbours.[((new System.Random()).Next(0,neighbours.Length))] <! EvaluatePushSum(sum,weight,n)

         | EvaluatePushSum (s:float,w,n) -> 
                          let  latestSum = sum+s
                          let latestWeight = weight + w
                          let ans = sum/weight - latestSum/latestWeight |> abs
                          if(ans >n) then
                            temp<- 0
                            sum <- sum+s
                            weight <- weight + w
                            sum <- sum/2.0
                            weight <- weight/2.0
                            neighbours.[((new System.Random()).Next(0,neighbours.Length))] <! EvaluatePushSum(sum,weight,n)
                           elif (temp>=3) then
                             handler<! PushSumResult(sum,weight)
                            else
                               sum<- sum/2.0
                               weight <- weight/2.0
                               temp<- temp+1
                               neighbours.[((new System.Random()).Next(0,neighbours.Length))] <! EvaluatePushSum(sum,weight,n)


         | _-> ()


let mutable numNodes = int(string (fsi.CommandLineArgs.GetValue 1))
let topology = string (fsi.CommandLineArgs.GetValue 2)
let protocol= string (fsi.CommandLineArgs.GetValue 3)

let system = ActorSystem.Create("System")

let mutable totalNodes=float(numNodes)

let handler=system.ActorOf(Props.Create(typeof<Handler>),"handler")

match topology  with 
      | "full"->
          let arr= Array.zeroCreate (numNodes+1)
          for i in [0..numNodes] do
              arr.[i]<-system.ActorOf(Props.Create(typeof<Node>,handler,i+1),""+string(i))
          for i in [0..numNodes] do
              arr.[i]<!Initialize(arr)
              
          let rand = System.Random().Next(0,numNodes)
          if protocol="gossip" then
            handler<!NumParticipants(numNodes)
            handler<!StartTimer(1)
            arr.[rand]<!BeginGossip("full")
          else if protocol="push-sum" then
            handler<!StartTimer(1)
            arr.[rand]<!BeginPushSum(10.0 ** -10.0)
        
      |"line"->
          let arr= Array.zeroCreate (numNodes)
          for i in [0..numNodes-1] do
              arr.[i]<-system.ActorOf(Props.Create(typeof<Node>,handler,i+1),""+string(i))
          for i in [0..numNodes-1] do
              if(i = 0) then
                let neighbourArray=[|arr.[1]|]
                arr.[i]<!Initialize(neighbourArray)
              else if(i = numNodes - 1) then
                let neighbourArray=[|arr.[numNodes-2]|]
                arr.[i]<!Initialize(neighbourArray)
              else
                let neighbourArray=[|arr.[(i-1)];arr.[(i+1)]|]
                arr.[i]<!Initialize(neighbourArray)
          let rand = System.Random().Next(0,numNodes)
          if protocol="gossip" then
            handler<!NumParticipants(numNodes)
            handler<!StartTimer(1)
            arr.[rand]<!BeginGossip("line")
          else if protocol="push-sum" then
            handler<!StartTimer(1)
            arr.[rand]<!BeginPushSum(10.0 ** -10.0)

      |"2D"->
           let gridSize=int(ceil(sqrt totalNodes))
           let totalGrid=gridSize*gridSize
           let arr= Array.zeroCreate (totalGrid)
           for i in [0..numNodes-1] do
              arr.[i]<-system.ActorOf(Props.Create(typeof<Node>,handler,i+1),""+string(i))
           
           for i in [0..gridSize-1] do
               for j in [0..gridSize-1] do
                    let k = i*gridSize+j
                    if(k < numNodes) then
                        let mutable neighbours:IActorRef[]=[||]
                        if (j+1<gridSize && k+1 < numNodes) then
                            neighbours<-(Array.append neighbours [|arr.[i*gridSize+j+1]|])
                        if j-1>=0 then
                            neighbours<-Array.append neighbours [|arr.[i*gridSize+j-1]|]
                        if i-1>=0 then
                            neighbours<-Array.append neighbours [|arr.[(i-1)*gridSize+j]|]
                        if (i+1<gridSize && k+gridSize < numNodes) then
                            neighbours<-(Array.append neighbours [|arr.[(i+1)*gridSize+j]|])
                        arr.[i*gridSize+j]<!Initialize(neighbours)
           let rand = System.Random().Next(0,numNodes)  
           if protocol="gossip" then
            handler<!NumParticipants(numNodes)
            handler<!StartTimer(1)
            arr.[rand]<!BeginGossip("2D")
           else if protocol="push-sum" then
            handler<!StartTimer(1)
            arr.[rand]<!BeginPushSum(10.0 ** -10.0)

       |"imp2D" ->
           let gridSize=int(ceil(sqrt totalNodes))
           let totalGrid=gridSize*gridSize
           let arr= Array.zeroCreate (numNodes)
           for i in [0..numNodes-1] do
              arr.[i]<-system.ActorOf(Props.Create(typeof<Node>,handler,i+1),""+string(i))
           for i in [0..gridSize-1] do
               for j in [0..gridSize-1] do
                    let k = i*gridSize+j
                    if(k < numNodes) then
                        let mutable neighbours:IActorRef[]=[||]
                        if (j+1<gridSize && k+1 < numNodes) then
                            neighbours<-(Array.append neighbours [|arr.[i*gridSize+j+1]|])
                        if j-1>=0 then
                            neighbours<-Array.append neighbours [|arr.[i*gridSize+j-1]|]
                        if i-1>=0 then
                            neighbours<-Array.append neighbours [|arr.[(i-1)*gridSize+j]|]
                        if (i+1<gridSize && k+gridSize < numNodes) then
                            neighbours<-(Array.append neighbours [|arr.[(i+1)*gridSize+j]|])
                        let mutable added = false
                        while(not added) do
                            let r = (new System.Random()).Next(0,numNodes)
                            if(r <> k && r <> k-1 && r <> k-gridSize && r <> k+1 && r <> k+gridSize) then
                                neighbours<-(Array.append neighbours [|arr.[r]|])
                                added <- true
                        arr.[i*gridSize+j]<!Initialize(neighbours)


           let rand = (new System.Random()).Next(0,numNodes)  
           if protocol="gossip" then
            handler<!NumParticipants(numNodes)
            handler<!StartTimer(1)
            arr.[rand]<!BeginGossip("imp2D")
           else if protocol="push-sum" then
            handler<!StartTimer(1)
            arr.[rand]<!BeginPushSum(10.0 ** -10.0)
      | _-> ()
System.Console.ReadLine()|>ignore