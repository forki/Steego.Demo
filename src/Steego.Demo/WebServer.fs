module Steego.Demo.WebServer

//  Encapsulate everything in 
open Suave

type private WebInstance() = 
    let started = ref false
    let mainWebPart = ref (Successful.OK "Welcome")

    member this.Update(newPart) = lock mainWebPart (fun () -> mainWebPart := newPart)

    member this.Start() = 
        if not started.Value then
            lock started (fun () -> started := true)
            async {
                    startWebServer defaultConfig (fun(ctx : HttpContext) ->
                        async {
                            let part = lock mainWebPart (fun () -> mainWebPart.Value)
                            return! part ctx
                        })
            } |> Async.Start

let private instance = lazy WebInstance()
let update(newPart) = instance.Value.Update(newPart)
let start() = instance.Value.Start()