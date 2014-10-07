open System
open System.Net
open Oars

type IEcho =
   inherit IDisposable
   abstract member Post : EchoMsg -> unit
and EchoMsg =
  | Read of byte array
  | Write of byte array

type ConnectionMsg =
  | New of IEcho
  | EchoAll of byte array * IEcho

let eventBase = new EventSyncContext()

let connectionsAgent = MailboxProcessor<ConnectionMsg>.Start(fun agent -> 
  let rec work clist = async{
    let! m = agent.Receive()
    match m with
    | New n -> return! work(n::clist)
    | EchoAll (b,p) -> do clist 
                        |> List.filter(fun (i) -> not (Object.ReferenceEquals(i,p))) 
                        |> List.iter(fun (i) -> i.Post (Write b))
                       return! work(clist)
  }
  work([]))

type Echo(s:BufferEvent, esc:EventSyncContext) as self =
  let echoAgent = new MailboxProcessor<EchoMsg>(fun agent -> 
    let rec work() = async{
      let! m = agent.Receive()
      match m with
        | Read b -> connectionsAgent.Post (EchoAll (b,self))
                    return! work()
        | Write b ->  esc.Post((fun (state) -> 
                      try
                        s.Output.Add(b, 0, b.Length) |> ignore
                      with
                        |e -> printfn "Could not write %A" e.Message 
                      ), null)
                      return! work()}

    work())
  interface IEcho with 
    member x.Post m = echoAgent.Post m
    member x.Dispose () =
      s.Dispose()
      (echoAgent :> IDisposable).Dispose()

let read s remoteIP =
  let proxy = new Echo(s,eventBase) :> IEcho
  let buffer = Array.zeroCreate 65536
  s.Read.Add(fun (e) -> try
                          let input = s.Input
                          let readall() =
                            let rec readallinternal() =
                              let read = input.Remove(buffer, 0, buffer.Length)
                              match read with
                                | n when n > 0 -> let a:byte array = Array.zeroCreate (read)
                                                  Buffer.BlockCopy(buffer, 0, a, 0, read)
                                                  proxy.Post (Read a)
                                                  readallinternal()
                                |_ -> ()
                            readallinternal()
                          readall()
                        with
                          |e -> printfn "Could not read %A" e.Message )

  s.Enable()

let tcp_ip_proxy listenIp listenPort = 
  let endpoint = new IPEndPoint(IPAddress.Parse(listenIp), listenPort)
  let listener = new ConnectionListener(eventBase.EventBase, endpoint, 50s)
  listener.ConnectionAccepted.Add(fun (s) ->  let be = new BufferEvent(eventBase.EventBase, s.Socket, 60000)
                                              read be s.RemoteEndPoint)
  printfn "Listening OK..."
  eventBase.RunOnCurrentThread()

[<EntryPoint>]
let main argv = 
  printfn "F# started on %A" argv
  tcp_ip_proxy argv.[0] (Int32.Parse(argv.[1])) |> ignore
  0

