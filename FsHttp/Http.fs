
module FsHttp

open System
open System.Net.Http


type Header = {
    url: string;
    method: HttpMethod;
    headers: (string*string) list;
}

type Content = {
    content: string;
    contentType: string;
    headers: (string*string) list;
} 


type StartingContext = StartingContext


type FinalContext = {
    request: Header;
    content: Content option;
}

let invoke (context:FinalContext) =
    let request = context.request
    let requestMessage = new HttpRequestMessage(request.method, request.url)
    
    requestMessage.Content <-
        match context.content with
        | Some c -> 
            let stringContent = new StringContent(c.content, System.Text.Encoding.UTF8, c.contentType)
            for name,value in c.headers do
                stringContent.Headers.TryAddWithoutValidation(name, value) |> ignore
            stringContent
        | _ -> null
    
    for name,value in request.headers do
        requestMessage.Headers.TryAddWithoutValidation(name, value) |> ignore
    
    use client = new HttpClient()
    client.SendAsync(requestMessage).Result

// let run (context:FinalContext) =
//     let response = context |> invoke
//     let content = response.Content.ReadAsStringAsync().Result
//     printfn "%A" content



type HeaderContext = {
    request: Header;
} with
    static member header (this:HeaderContext, name:string, value:string) = this
    static member run (this:HeaderContext) =
        let finalContext = { request=this.request; content=None }
        invoke finalContext

type BodyContext = {
    request: Header;
    content: Content;
} with
    static member header (this:BodyContext, name:string, value:string) = this
    static member run (this:BodyContext) =
        let finalContext:FinalContext = { request=this.request; content=Some this.content }
        invoke finalContext

let inline run (context:^t) =
    let response = (^t: (static member run: ^t -> HttpResponseMessage) (context))
    let content = response.Content.ReadAsStringAsync().Result
    content


type HttpBuilder() =

    let initializeRequest (context:StartingContext) (url:string) (method:HttpMethod) =
        let formattedUrl =
            url.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
            |> Seq.map (fun x -> x.Trim())
            |> Seq.filter (fun x -> not (x.StartsWith("//")))
            |> Seq.reduce (+)
        {
            request = { url=formattedUrl; method=method; headers=[] }
        }

    member this.Bind(m, f) = f m
    member this.Return(x) = x
    member this.Yield(x) = StartingContext
    member this.For(m, f) = this.Bind m f

    [<CustomOperation("GET")>]
    member this.Get(StartingContext, url:string) =
        initializeRequest StartingContext url HttpMethod.Get
    [<CustomOperation("PUT")>]
    member this.Put(StartingContext, url:string) =
        initializeRequest StartingContext url HttpMethod.Put
    [<CustomOperation("POST")>]
    member this.Post(StartingContext, url:string) =
        initializeRequest StartingContext url HttpMethod.Post

    [<CustomOperation("header")>]
    member inline this.Header(context:^t, name, value) =
        (^t: (static member header: ^t * string * string -> ^t) (context,name,value))
        
    [<CustomOperation("body")>]
    member this.Body(context:HeaderContext) : BodyContext =
        {
            request = context.request;
            content = { content=""; contentType=""; headers=[] }
        }
    
    [<CustomOperation("json")>]
    member this.Json(context:BodyContext, json:string) =
        let content = context.content
        { context with
            content = { content with content=json; contentType="application/json";  }
        }

let http = HttpBuilder()



http {
   POST @"http://www.google.de"
   header "a" "b"

   body
   header "c" "d"
   json """
   {
       "name": "hsfsdf sdf s"
   }
   """
}
|> run
|> printfn "%A"
