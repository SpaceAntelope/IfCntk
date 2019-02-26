#r @"..\bin\Cntk.Core.Managed-2.6.dll"
#r "netstandard"

module PrepareWorkspace =
    open System
    open System.IO
    open System.Reflection

    // Assuming all the cntk bins are in one path together
    // See github/prepareworkspace.fsx
    let CntkCoreManagedPath =
        let IsWindows =
            System.Runtime.InteropServices.RuntimeInformation.OSDescription.Contains
                ("Windows")
        let codebase = Assembly.GetAssembly(typeof<CNTK.CNTKLib>).CodeBase
        match IsWindows with
        | true -> codebase.Replace("file:///", "")
        | false -> codebase.Replace("file://", "")
        |> Path.GetDirectoryName

    Environment.GetEnvironmentVariable("PATH")
    |> fun path ->
        sprintf "%s%c%s" path (Path.PathSeparator)
            (Path.GetFullPath(CntkCoreManagedPath))
    |> fun path -> Environment.SetEnvironmentVariable("PATH", path)

open CNTK

type Tensor =
    | Fun of Function
    | Var of Variable
    | Par of Parameter

    member this.AsVar() =
        match this with
        | Var v -> v
        | Fun f -> new Variable(f)
        | Par p -> p :> Variable

    member this.AsFun() =
        match this with
        | Fun f -> f
        | Var v -> v.ToFunction()
        | Par p -> p.ToFunction()

    member this.Uid =
        match this with
        | Fun f -> f.Uid
        | Var v -> v.Uid
        | Par p -> p.Uid

    member this.AsString() =
        match this with
        | Fun f -> f.AsString()
        | Var v -> v.AsString()
        | Par p -> p.AsString()

    member this.IsParameter =
        match this with
        | Fun _ -> false
        | Var v -> v.IsParameter
        | Par _ -> true

    member this.Decompose =
        let visited = System.Collections.Generic.Dictionary<string, Tensor>()

        let rec expand (t : Tensor) =
            match visited.ContainsKey(t.Uid) with
            | true -> Seq.empty
            | false ->
                visited.Add(t.Uid, t)
                seq {
                    match t with
                    | Var v ->
                        yield if v.IsParameter then Par(new Parameter(v))
                              else Var v
                        if v.Owner
                           |> isNull
                           |> not
                        then yield Fun v.Owner
                    | Fun f ->
                        yield Fun f
                        yield! seq {
                                   yield! f.Inputs |> Seq.map Var
                                   yield! f.Outputs |> Seq.map Var
                               }
                               |> Seq.collect expand
                }
        Array.ofSeq (expand this)

// (Fun model).Decompose
// |> Array.map (fun f -> f.AsString())
// |> Array.iter (printfn "%s")
type Function with
    member this.Decompose =
        let root = this
        let visited = System.Collections.Generic.Dictionary<string, Function>()

        let rec expand (f : Function) =
            match visited.ContainsKey(f.Uid) with
            | true -> Seq.empty
            | false ->
                visited.Add(f.Uid, f)
                seq {
                    yield f
                    yield! seq {
                               yield! f.Inputs
                               yield! f.Outputs
                           }
                           |> Seq.map (fun v -> v.Owner)
                           |> Seq.filter (not << isNull)
                           |> Seq.collect expand
                }
        Array.ofSeq (expand root)

module Graphviz =
    open System

    let varText (v : Variable) =
        if String.IsNullOrEmpty v.Name then v.Uid
        else v.Name

    let funText (f : Function) =
        if String.IsNullOrEmpty f.Name then f.Uid
        else f.Name

    let varLabel (v : Variable) = sprintf "%s [label=\"%s\"];" v.Uid (varText v)
    let funLabel (f : Function) = sprintf "%s [label=\"%s\"];" f.Uid (funText f)
    let edgeLabel (v : Variable) =
        v.AsString().Replace("(", "\n").Replace(")", "").Replace("'", "")
         .Replace("->", "\n->\n") |> sprintf "[label=\"%s\"]"

    let varShape (v : Variable) =
        match v with
        | _ when v.IsInput -> sprintf "%s [shape=invhouse, color=yellow];" v.Uid
        | _ when v.IsOutput -> sprintf "%s [shape=invhouse, color=gray];" v.Uid
        | _ when v.IsPlaceholder ->
            sprintf "%s [shape=invhouse, color=yellow];" v.Uid
        | _ when v.IsParameter ->
            sprintf "%s [shape=diamond, color=green];" v.Uid
        | _ when v.IsConstant ->
            sprintf "%s [shape=rectangle, color=lightblue];" v.Uid
        | _ -> sprintf "%s [shape=circle, color=purple];" v.Uid

    let funShape (f : Function) =
        match f with
        | _ when f.IsComposite ->
            sprintf
                "%s [shape=ellipse, fontsize=20, penwidth=2, peripheries=2];"
                f.Uid
        | _ when f.IsPrimitive ->
            sprintf "%s [shape=ellipse, fontsize=20, penwidth=2, size=0.6];"
                f.Uid
        | _ -> sprintf "%s [shape=ellipse, fontsize=20, penwidth=4];" f.Uid

    let varEdges (f : Function) (v : Variable) =
        let inputIndex =
            f.Inputs
            |> Seq.map (fun v -> v.Uid)
            |> Set

        let outputIndex =
            f.Outputs
            |> Seq.map (fun v -> v.Uid)
            |> Set

        match inputIndex.Contains(v.Uid), outputIndex.Contains(v.Uid) with
        | true, _ when v.IsParameter ->
            sprintf "%s -> %s %s;" v.Uid f.Uid (edgeLabel v) |> Some
        | _, true when v.IsParameter ->
            sprintf "%s -> %s [label=\"output param\"];" f.Uid v.Uid |> Some
        | true, _ -> sprintf "%s -> %s %s;" v.Uid f.Uid (edgeLabel v) |> Some
        | _, true -> sprintf "%s -> %s [label=\"output\"];" f.Uid v.Uid |> Some
        | _ -> None

    let varOwner (v : Variable) =
        match v.Owner with
        | null -> None
        | _ -> sprintf "%s -> %s [style=\"dotted\"];" v.Owner.Uid v.Uid |> Some

    let extractDotNotation (f : Function) =
        let vars = Seq.append f.Inputs f.Outputs

        let funs =
            seq {
                yield f
                yield f.RootFunction
                yield! vars
                       |> Seq.map (fun v -> v.Owner)
                       |> Seq.filter (isNull >> not)
            }
        seq {
            if f.Uid <> f.RootFunction.Uid then
                yield sprintf "%s -> %s [label=\"root function\"];"
                          f.RootFunction.Uid f.Uid
            yield! vars |> Seq.map varShape
            yield! vars |> Seq.map varLabel
            yield! vars
                   |> Seq.map (varEdges f)
                   |> Seq.choose id
            //yield! vars |> Seq.map varOwner |> Seq.choose id
            yield! funs |> Seq.map funLabel
            yield! funs |> Seq.map funShape
        }
        |> Seq.distinct

let dotNotation (model : Function) =
    model.Decompose
    |> Array.filter (fun f -> not f.IsComposite)
    |> Array.collect (Graphviz.extractDotNotation >> Array.ofSeq)
    |> Array.distinct
    |> Array.reduce (sprintf "%s\n%s")

//|> sprintf "digraph { %s }"
module Node =
    open System.Collections.Generic
    open System.Reflection
    open System

    /// Active pattern to separate properties for special handling.
    let (|IsEnumerable|IsDescribable|IsPrimitive|) (t : Type) =
        if t <> typeof<string> && (typeof<IEnumerable<_>>).IsAssignableFrom(t) then
            IsEnumerable
        else if t.GetMethods()
                |> Array.exists (fun meth -> meth.Name = "AsString") then
            IsDescribable
        else IsPrimitive

    /// Helper function to convert any property to string,
    /// always checking if .AsString() is available
    let asString item =
        if isNull item then ""
        else
            match item.GetType() with
            | IsDescribable ->
                item.GetType().GetMethod("AsString", Array.empty)
                    .Invoke(item, Array.empty).ToString()
            | _ -> item.ToString()

    /// A simple property serializer for CNTK nodes
    /// <remarks> CNTK Helper function </remarks>
    let Describe(item : obj) =
        [| yield ("NodeType", item.GetType().Name)
           yield ("AsString", item |> asString)
           yield! item.GetType().GetProperties()
                  |> Seq.map (fun prop ->
                         match prop.PropertyType with
                         | IsEnumerable ->
                             prop.Name,
                             (prop.GetValue(item) :?> IEnumerable<_>)
                             |> function
                             | list when list |> Seq.isEmpty -> "[]"
                             | list ->
                                 list
                                 |> Seq.map (asString)
                                 |> Seq.reduce (sprintf "%s, %s")
                         | IsDescribable ->
                             prop.Name, prop.GetValue(item) |> asString
                         | IsPrimitive ->
                             prop.Name,
                             try
                                 prop.GetValue(item) |> asString
                             with ex -> sprintf "%s" ex.Message) |]
        |> Array.map (fun (key, value) -> sprintf "\"%s\": \"%s\"" key value)
        |> Array.reduce (sprintf "%s, %s") //   sprintf "%s, \"%s\": \"%s\"" state key value) ""

    let modelInfo (model : Function) =
        let funcs = model.Decompose |> Array.filter (fun f -> not f.IsComposite)

        let vars =
            funcs
            |> Array.collect (fun f ->
                   [| yield! f.Inputs
                      yield! f.Outputs |])
            |> Array.distinctBy (fun v -> v.Uid)
        [| yield "{"
           yield funcs
                 |> Array.map
                        (fun f -> sprintf "\"%s\": { %s }" (f.Uid) (Describe f))
                 |> Array.reduce (sprintf "%s,\n%s")
           yield ","
           yield vars
                 |> Array.map
                        (fun v -> sprintf "\"%s\": { %s }" (v.Uid) (Describe v))
                 |> Array.reduce (sprintf "%s,\n%s")
           yield "}" |]
        |> Array.reduce (sprintf "%s\n%s")
        |> fun text -> text.Trim()

let html dot info =
    """
<!DOCTYPE html>
<meta charset="utf-8">
<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css">
<div id="NodeInfo" class="draggable card text-white bg-info mb-3" style="max-width: 18rem;">
  <div id="NodeInfoHeader" class="card-header">Header</div>
  <div class="card-body">
    <h5 class="card-title">Info card title</h5>
    <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
  </div>
</div>
<div id="NodeInfo2" class="draggable card text-white bg-dark mb-3" style="max-width: 18rem; ">
  <div id="NodeInfo2Header" class="card-header">Header</div>
  <div class="card-body">
    <h5 class="card-title">Info card title</h5>
    <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
  </div>
</div>
<div id="graph" style="width: 1920px;height:1080px"></div>
<script src="http://d3js.org/d3.v5.min.js"></script>
<script src="https://unpkg.com/viz.js@1.8.0/viz.js" type="javascript/worker"></script>
<script src="https://unpkg.com/d3-graphviz@1.3.1/build/d3-graphviz.min.js"></script>
<script src="https://ajax.googleapis.com/ajax/libs/jquery/3.3.1/jquery.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.7/umd/popper.min.js"></script>
<script src="https://maxcdn.bootstrapcdn.com/bootstrap/4.3.1/js/bootstrap.min.js"></script>
<script src="https://unpkg.com/interactjs@1.3.4/dist/interact.min.js"></script>
<script>
    //dragElement(document.getElementById("NodeInfo"));
    //dragElement(document.getElementById("NodeInfo2"));

$(()=>{

    interact('.draggable').draggable({})
});

  var dot = `
  digraph {
    {DOT}
  }`;

  var info = {INFO};

  var viz = d3
    .select("#graph")
    .graphviz()
    //.logEvents(true)
    .on("initEnd", function () {

        //viz.engine("circo");
        viz.renderDot(dot)
            .on("end", function () {

              let svg = d3.select("svg");//.attr("width", "1750pt").attr("height","1000pt");

              let defs = svg.append("defs");

              let filter = defs
                  .append("filter")
                  .attr("id", "shadow")
                  .attr("x", "-50%")
                  .attr("y", "-50%")
                  .attr("width", "200%")
                  .attr("height", "200%");

              filter.append("feGaussianBlur")
                .attr("in", "SourceAlpha")
                .attr("stdDeviation", 3)
                .attr("result", "blur");

              filter
                .append("feOffset")
                .attr("in", "blur")
                .attr("dx", 3)
                .attr("dy", 3);

              filter.append("feComponentTransfer")
                .append("feFuncA").attr("type", "linear")
                .attr("slope", 0.35);

              var merge = filter.append("feMerge");
              merge.append("feMergeNode");
              merge.append("feMergeNode").attr("in", "SourceGraphic");

              d3.selectAll(".node ellipse, .node polygon")
                .style("fill", "white")
                .on("mouseover", (d, i, n) => d3.select(n[i]).style("filter", "url(#shadow)"))
                .on("mouseout", (d, i, n) => d3.select(n[i]).style("filter", null));
        });
  });
</script>
    """
        .Replace("{DOT}", dot)
        .Replace("{INFO}", info)

let showGraph (path : string) =
    let model = Function.Load(path, DeviceDescriptor.CPUDevice)
    (dotNotation model, Node.modelInfo model)
    ||> html
    |> fun txt ->
        let fname = System.IO.Path.GetTempFileName() + ".html"
        System.IO.File.WriteAllText(fname, txt)
        fname
    |> System.Diagnostics.Process.Start
    |> ignore

showGraph "sample2x25x25x2.model"
// classifier.Model.toFun
// |> createGraphVizDiagram
// |> html
// |> fun txt ->
//         let fname = System.IO.Path.GetTempFileName() + ".html"
//         System.IO.File.WriteAllText(fname,txt)
//         fname
// |> System.Diagnostics.Process.Start
// |> ignore
try
    (Function.Load
         ("sample.model", DeviceDescriptor.CPUDevice, ModelFormat.CNTKv2)).Decompose
    |> Array.iter (fun f -> printfn "%s" <| f.AsString())
with ex -> printfn "%s" ex.Message

open System.Linq

let paramData<'T> (p : CNTK.Parameter) =
    let arrayView = p.Value()
    let value = new Value(arrayView)
    value.GetDenseData<'T>(p)

let simpleModel = Function.Load("sample.model", DeviceDescriptor.CPUDevice)
let model = Function.Load("sample2x25x10x2.model", DeviceDescriptor.CPUDevice)

Function.Load("sample2x25x10x2.model", DeviceDescriptor.CPUDevice)
|> Fun
|> fun model -> model.Decompose
|> Array.choose (function
       | Par p -> Some p
       | _ -> None)
|> Array.map (fun p ->
       printfn "%d %s" (p.Shape.Rank) (p.AsString())
       p.Uid,
       p
       |> paramData<float32>
       |> Seq.head
       |> Array.ofSeq
       |> Array.chunkBySize (p.Shape.[p.Shape.Rank - 1]))
|> Array.ofSeq
