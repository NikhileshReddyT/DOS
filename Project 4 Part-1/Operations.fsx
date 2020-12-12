#r "nuget: Akka" 
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote" 
#r "nuget: Akka.TestKit" 

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let mutable SubscribersMap: Map<string, list<string>> = Map.empty
let mutable SubscribedMap: Map<string, list<string>> = Map.empty
let mutable HTmap: Map<string, list<string>> = Map.empty
let mutable AtMap: Map<string, list<string>> = Map.empty
let mutable bufferMap: Map<string, list<string>> = Map.empty
let mutable usrSet: Set<string> = Set.empty
let mutable usrMap:  Map<string, string> = Map.empty
let mutable activeUsrMap:  Map<string, bool> = Map.empty

let setTweet (Msg: string) (Usrname: string) (subscriber: string) =
        let curr = bufferMap.Item subscriber
        let temp = "author:" + Usrname + " = " + Msg
        bufferMap <- bufferMap.Add(subscriber, curr @ [ temp ])

let hashtagQuery (Msg: string) =
    let a = Msg.Split(' ') |> Array.toList
    let mutable HTlist : List<string> = List.Empty
    for i in a do
        if i.StartsWith '#' then
            HTlist <- List.append HTlist [i]
    HTlist

let setHT (htList: list<string>) (Msg: string) =
    for i in htList do
        if not (HTmap.ContainsKey i) then
            HTmap <- HTmap.Add(i, [ Msg ])
        else
            let curr = HTmap.Item i
            HTmap <- HTmap.Add(i, curr @ [ Msg ])

let UserActivation (uid: string) (stat: bool) =
    activeUsrMap <- activeUsrMap.Add(uid, stat)

let userRegistration (id: string) (Pwd: string) =
    let mutable response = ""
    if (usrSet.Contains id) then
        response <- "user already exists"
    else 
        usrSet <- usrSet.Add(id)
        usrMap <- usrMap.Add(id,Pwd)
        activeUsrMap <- activeUsrMap.Add(id,true)
        AtMap <- AtMap.Add(id, [])
        SubscribedMap <- SubscribedMap.Add(id, List.empty)
        SubscribersMap <- SubscribersMap.Add(id, List.empty)
        bufferMap <- bufferMap.Add(id, [])
        response <- "registration is successful"
    response

let mentionQuery (Msg: string) =
    let a = Msg.Split(' ') |> Array.toList
    let mutable atList : List<string> = List.Empty
    for i in a do
        if i.StartsWith '@' then
            atList <- List.append atList [i]
    atList

let setMentions (atList: list<string>) (Msg: string) (senderUser: string) =
    for i in atList do
        if not (AtMap.ContainsKey i) then
            AtMap <- AtMap.Add(i, [ Msg ])
        else
            let curr = AtMap.Item i
            AtMap <- AtMap.Add(i, curr @ [ Msg ])

let addSubscriber (uid: string) (Lid: string) =
    if((usrSet.Contains(uid)) && (usrSet.Contains(Lid))) then
        let mutable sub = SubscribersMap.Item Lid
        sub <- sub @ [ uid ]
        SubscribersMap <- SubscribersMap.Add(Lid, sub)
        let mutable lead = SubscribedMap.Item uid
        lead <- lead @ [ Lid ]
        SubscribedMap <- SubscribedMap.Add(uid, lead)
        true
    else 
        false

let setRetweet (Msg: string) (Usrname: string) (subscriber: string) =
        if not (Msg.StartsWith "Retweetedby") then
            let curr = bufferMap.Item subscriber
            let retweet = "Retweetedby:" + Usrname + "|" + Msg
            bufferMap <- bufferMap.Add(subscriber, curr @ [ retweet ])
        else
            let a = Msg.Split('|') |> Array.toList
            let curr = bufferMap.Item subscriber
            let retweet = "Retweetedby:" + Usrname + "|" + a.[1]
            bufferMap <- bufferMap.Add(subscriber, curr @ [ retweet ])

let feed (username: string) =
    let pendingFeed = bufferMap.Item username
    bufferMap <- bufferMap.Add(username, [ "start" ])
    pendingFeed

let userLoginCheck (uid: string) =
    let mutable flag = false
    if(activeUsrMap.ContainsKey uid) then
        if(activeUsrMap.Item uid) then
            flag <- true
    flag