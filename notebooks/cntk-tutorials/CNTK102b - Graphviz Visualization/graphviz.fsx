open CNTK
#r @"..\bin\Cntk.Core.Managed-2.6.dll"
#r "netstandard"

//open CNTK
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
    |> fun path -> sprintf "%s%c%s" path (Path.PathSeparator) (Path.GetFullPath(CntkCoreManagedPath))
    |> fun path -> Environment.SetEnvironmentVariable("PATH", path)

open System.Collections.Generic
open System.Reflection
open CNTK

type Function with
    member this.Decompose = 
        let root = this
        let visited = System.Collections.Generic.Dictionary<string, Function>()            
    
        let rec expand (f: Function) = 
            match visited.ContainsKey(f.Uid) with
            | true -> Seq.empty
            | false -> 
                visited.Add(f.Uid, f)
                seq {
                    yield f
                    yield! 
                        seq { yield! f.Inputs
                              yield! f.Outputs }
                        |> Seq.map (fun v -> v.Owner)      
                        |> Seq.filter (not<<isNull)
                        |> Seq.collect expand
                }        
    
        Array.ofSeq (expand root)

module GraphNodeStyle =
    open System
    let varText (v:Variable) = if String.IsNullOrEmpty v.Name then v.Uid else v.Name
    let funText (f: Function) = if String.IsNullOrEmpty f.Name then f.Uid else f.Name

    let varLabel (v: Variable) = sprintf "%s [label=\"%s\"];" v.Uid (varText v)
    let funLabel (f: Function) = sprintf "%s [label=\"%s\"];" f.Uid (funText f)
    let edgeLabel (v : Variable) = 
        v.AsString()
         .Replace("(","\n").Replace(")","")
         .Replace("'","").Replace("->","\n->\n")
        |> sprintf "[label=\"%s\"]"            

    let varShape (v: Variable) =
        match v with
        | _ when v.IsInput -> sprintf "%s [shape=invhouse, color=yellow];" v.Uid
        | _ when v.IsOutput -> sprintf "%s [shape=invhouse, color=gray];" v.Uid
        | _ when v.IsPlaceholder -> sprintf "%s [shape=invhouse, color=yellow];" v.Uid
        | _ when v.IsParameter -> sprintf "%s [shape=diamond, color=green];" v.Uid
        | _ when v.IsConstant -> sprintf "%s [shape=rectangle, color=lightblue];" v.Uid
        | _ -> sprintf "%s [shape=circle, color=purple];" v.Uid

    let funShape (f: Function) = 
        match f with 
        | _ when f.IsComposite -> sprintf "%s [shape=ellipse, fontsize=20, penwidth=2, peripheries=2];" f.Uid
        | _ when f.IsPrimitive -> sprintf "%s [shape=ellipse, fontsize=20, penwidth=2, size=0.6];" f.Uid
        | _ -> sprintf "%s [shape=ellipse, fontsize=20, penwidth=4];" f.Uid

    let varEdges (f: Function) (v: Variable) = 
        let inputIndex = f.Inputs |> Seq.map (fun v -> v.Uid) |> Set
        let outputIndex = f.Outputs |> Seq.map (fun v -> v.Uid) |> Set

        match inputIndex.Contains(v.Uid), outputIndex.Contains(v.Uid) with 
        | true, _ when v.IsParameter -> sprintf "%s -> %s %s;" v.Uid f.Uid (edgeLabel v) |> Some
        | _, true when v.IsParameter -> sprintf "%s -> %s [label=\"output param\"];" f.Uid v.Uid|> Some
        | true, _ -> sprintf "%s -> %s %s;" v.Uid f.Uid (edgeLabel v) |> Some 
        | _, true -> sprintf "%s -> %s [label=\"output\"];" f.Uid v.Uid |> Some            
        | _ -> None

    let varOwner (v: Variable) =
        match v.Owner with
        | null -> None
        | _ -> sprintf "%s -> %s [style=\"dotted\"];" v.Owner.Uid v.Uid |> Some

let extractGraphVizDotNotation (f: Function) =     
    let vars = Seq.append f.Inputs f.Outputs
    let funs = seq { 
            yield f
            yield f.RootFunction;
            yield! vars |> Seq.map (fun v -> v.Owner) |> Seq.filter (isNull>>not) 
        } 

    seq {        
        if f.Uid <> f.RootFunction.Uid 
        then yield sprintf "%s -> %s [label=\"root function\"];" f.RootFunction.Uid f.Uid
        yield! vars |> Seq.map GraphNodeStyle.varShape
        yield! vars |> Seq.map GraphNodeStyle.varLabel
        yield! vars |> Seq.map (GraphNodeStyle.varEdges f) |> Seq.choose id
        //yield! vars |> Seq.map varOwner |> Seq.choose id
        yield! funs |> Seq.map GraphNodeStyle.funLabel 
        yield! funs |> Seq.map GraphNodeStyle.funShape
    } |> Seq.distinct

let dotNotation (model:Function) = 
    model.Decompose
    |> Array.filter (fun f -> not f.IsComposite)
    |> Array.collect (extractGraphVizDotNotation>>Array.ofSeq)
    |> Array.distinct 
    |> Array.reduce(sprintf "%s\n%s")
    //|> sprintf "digraph { %s }"


let html dot = 
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
    """.Replace("{DOT}", dot)
    
let tryLoadModelWithFormat (format: ModelFormat) (path: string) =
    if (System.IO.File.Exists>>not) path 
    then failwith (sprintf "File '%s' not found" path)



let showGraph (path :string) =
    Function
        .Load(path, DeviceDescriptor.CPUDevice)
    |> dotNotation
    |> html
    |> fun txt -> 
        let fname = System.IO.Path.GetTempFileName() + ".html"
        System.IO.File.WriteAllText(fname,txt)
        fname 
    |> System.Diagnostics.Process.Start
    |> ignore

showGraph "sample.model"    

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
    Function.Load("sample.model", DeviceDescriptor.CPUDevice, ModelFormat.CNTKv2).Decompose 
    |> Array.iter (fun f -> printfn "%s" <| f.AsString())
with ex -> printfn "%s" ex.Message
