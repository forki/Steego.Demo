﻿module Steego.Printer

open System
open System.Text
open System.Collections
open System.Linq
open TypeInfo
open Steego.Type.Patterns.TypePatterns
open Steego.Type.Patterns.ObjectPatterns
open Steego.Reflection.Navigators
open Steego.Demo

let a = Html.makeTag "a"
let div = Html.makeTag "div"
let table = Html.makeTag "table"
let thead = Html.makeTag "thead"
let tbody = Html.makeTag "tbody"
let tr = Html.makeTag "tr"
let th = Html.makeTag "th"
let td = Html.makeTag "td"

let getTypeName(o:obj) = Html.Text( sprintf "Not matched: %s" (o.GetType().FullName))

let keyValueRow(name:string, value:Html.Tag) = 
    tr [] [ 
        th [] [ Html.Text(name) ]
        td [] [ value ] 
    ]

let hkeyValueRow(name:Html.Tag, value:Html.Tag) = 
    tr [] [ 
        th [] [ name ]
        td [] [ value ] 
    ]

let link(path:string list)(text:string) = 
    let pathArray = path |> List.map Html.Encoders.url |> List.rev |> List.toArray
    let url = String.Join("/", pathArray)
    a [("href", "/" + url)] [ Html.Text(text) ]

let info = TypeInfo(null)

let maxTake = 100

module Patterns = begin
    let htmlObjectType = typeof<Html.IHtmlObject>
    
    open Fasterflect
    
    let (|HtmlObject|_|) (o:obj)  = 
        let t : Type = if not (isNull o) then o.GetType() else null
        if t = htmlObjectType || t.Implements(htmlObjectType) then 
            Some(o :?> Html.IHtmlObject) 
        else 
            None

end

open Patterns

let rec printHtml (level:int) = 
    
    let rec print (level:int) (o:obj) : Html.Tag = 
        match o with
        | null -> Html.Text("<null>")
        | HtmlObject(h) -> h.ToHtml
        | :? Html.Tag as t -> t
        | :? int as v -> Html.Text(v.ToString())
        | :? String as s -> Html.Text(s)
        | :? DateTime as v -> Html.Text(v.ToShortDateString())
        | IsPrimitive(n) -> Html.Text(n.ToString())
        | IsNullable(n) -> Html.Text(n.ToString())
        | :? NavContext as n ->
            match n.Value with
            | null -> Html.Text("<null>")
            | HtmlObject(h) -> h.ToHtml
            | :? Html.Tag as t -> t
            | IsPrimitive(n) -> Html.Text(n.ToString())
            | GenericList(Primitive(_), _, list) -> printList (level - 1) list
            | GenericList(_, getters, list) -> printGenericNavList (level - 1)  getters list n
            | Object(members, _, _, _, _) -> printNavObject (level - 1) members n
            | o -> print level o
        | GenericList(_) when level < 1 -> Html.Text("List...")
        | :? System.Collections.IEnumerable when level < 1 -> Html.Text("List...")
        | Object(_) when level < 1 -> Html.Text("Object...")
        | _ when level < 1 -> Html.Text("...")
        | GenericList(Primitive(_), _, list) -> printList (level - 1) list
        | GenericList(_, getters, list) -> printGenericList (level - 1)  getters list
        | :? System.Collections.IEnumerable as s -> printList (level - 1) s
        | Object(members, _, _, _, _) -> printObject (level - 1) members o
        | _ -> Html.Text("...")         

    and printNavObject (level:int) (_:MemberGetter list) (n:NavContext) =
            let o = n.Value
            let typeInfo = TypeInfo(o)
            let primativeMembers = typeInfo.PrimitiveMembers
            table 
                [("class", "navObject table table-bordered table-striped")]
                [
                    yield th [("colspan", "2")] [ Html.Text(typeInfo.Name) ] 
                    //  Show primitive members
                    for g in primativeMembers do
                        let value = g.Get(o) |> print level
                        yield keyValueRow(g.Name, value)
                    //  Show object members
                    for g in typeInfo.ObjectMembers do
                        let value = g.Get(o) |> print level
                        let o = n|> addPath(g.Name, value)
                        let memberLink = link o.Path g.Name 
                        yield hkeyValueRow(memberLink, memberLink)
                    //  Show enumerable members
                    for g in typeInfo.EnumerableMembers do
                        let value = g.Get(o) |> print level
                        let o = n |> addPath(g.Name, value)
                        let memberLink = link o.Path g.Name  
                        yield tr [] [ 
                            th [("colspan", "1")] [ memberLink ] 
                            td [("colspan", "1")] [ value ]
                        ]
                ]

    and printObject (level:int) (_:MemberGetter list) (o:obj) =
        let typeInfo = TypeInfo(o)
        let primativeMembers = typeInfo.PrimitiveMembers
        table 
            [("class", "simpleObj table table-bordered table-striped")]
            [
                yield th [("colspan", "2")] [ Html.Text(typeInfo.Name) ] 
                //  Show primitive members
                for g in primativeMembers do
                    let value = g.Get(o) |> print level
                    yield keyValueRow(g.Name, value)
                //  Show object members
                for g in typeInfo.ObjectMembers do
                    let value = g.Get(o) |> print level
                    yield keyValueRow(g.Name, value)
                //  Show enumerable members
                for g in typeInfo.EnumerableMembers do
                    let value = g.Get(o) |> print level
                    yield tr [] [ 
                        th [("colspan", "1")] [ Html.Text(g.Name) ] 
                        td [("colspan", "1")] [ value ]
                    ]
            ]

    and printList (level:int) (list:IEnumerable) = 
        let list = seq { for o in list -> o }
        table [("class", "simpleList table table-bordered table-striped")]
              [  for item in list.Take(maxTake) do
                    let value = item |> print level
                    yield tr [] [ td [] [ value ] ] 
              ]

    and printGenericNavList (level:int) (getters:MemberGetter list) (list:IEnumerable) (n:NavContext) =
        let list = seq { for o in list -> o }
        table 
            [("class", "genericNavList table table-bordered table-striped")]
            [ thead [] [
                            yield th [] [ Html.Text("") ]
                            for g in getters do 
                                yield th [] [ Html.Text(g.Name) ] 
                       ]
              tbody [] [ for (i,item) in list.Take(maxTake) |> Seq.indexed do
                            let child = n|> addPath(string(i), item)
                            let indexLink = link child.Path (string(i))
                            yield tr [] [ yield td [] [ indexLink ]
                                          for g in getters do
                                            let value = g.Get(item) |> print level 
                                            yield td [] [ value ]
                                        ] 
                       ]
            ]

    and printGenericList (level:int) (getters:MemberGetter list) (list:IEnumerable) =
        let list = seq { for o in list -> o }
        table 
            [("class", "genericList table table-bordered table-striped")]
            [ thead [] [ for g in getters -> th [] [ Html.Text(g.Name) ] ]
              tbody [] [ for item in list.Take(maxTake) do
                            yield tr [] [ for g in getters do
                                            let value = g.Get(item) |> print level 
                                            yield td [] [ value ]
                                        ] 
                       ]
            ]
    print level

let print level o = (printHtml level o).ToString()

