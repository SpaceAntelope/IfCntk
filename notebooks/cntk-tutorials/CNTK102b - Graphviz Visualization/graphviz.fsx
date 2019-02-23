open CNTK
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
    |> fun path -> sprintf "%s%c%s" path (Path.PathSeparator) (Path.GetFullPath(CntkCoreManagedPath))
    |> fun path -> Environment.SetEnvironmentVariable("PATH", path)

//System.Environment.GetEnvironmentVariable("PATH")
//PrepareWorkspace.CntkCoreManagedPath

open CNTK

let k = CNTKLib.Softmax(Variable.InputVariable(NDShape.CreateNDShape [|7|], DataType.Float))

System.IO.File.WriteAllBytes("s.model", k.Save())

k.Save("s.model")

Function.Load(@"E:\repos\DS-AI\IfCntk\notebooks\cntk-tutorials\CNTK102 - Hidden Layers\nonlinear.model", DeviceDescriptor.CPUDevice)

System.IO.Path.GetFullPath("nonlinear.model") 

let model = Function.Load(@"E:\repos\DS-AI\IfCntk\sample.model", DeviceDescriptor.CPUDevice), ModelFormat.ONNX)

Function.Load(@"E:\repos\DS-AI\IfCntk\notebooks\cntk-tutorials\CNTK102b - Graphviz Visualization\sample.model", DeviceDescriptor.CPUDevice)

open System.Collections.Generic
open System.Reflection

type C = CNTKLib

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


let model = Function.Load("sample.model", DeviceDescriptor.CPUDevice)

model.decompose |> Array.iter (fun f -> printfn "%s" <| f.AsString())

// let classifier = 
//     let options = {
//         ModelName = "Iris"
//         Device = CNTK.DeviceDescriptor.CPUDevice
//         DataType = CNTK.DataType.Float
//         InputLength = 2
//         OutputLength = 2
//         HiddenLayerDimensions = [|5|]
//         WeightInitialization = CNTK.FSharp.Core.Initializer.GlorotUniform
//         ActivationFunction =  CNTK.FSharp.Core.Tensor.sigmoid
//     }

//     FFNNClassifier(options)
       


// let createGraphVizDiagram (f:Function) =
//     f 
//     |> GraphUtils.decomposeFunction 
//     |> Seq.cast<KeyValuePair<string,Tensor>> 
//     |> Seq.map(fun pair -> pair.Value) 
//     |> Seq.where (function Fun _ -> true | _ -> false)
//     |> Seq.where (fun f -> f.toFun.IsComposite |> not )
//     |> Seq.collect (fun (Fun f) -> GraphUtils.extractGraphVizDotNotation f)
//     |> Seq.distinct //|> Array.ofSeq |> Array.filter (fun str -> str.Contains("->")) |> Array.sort
//     |> Seq.sort
//     |> Seq.reduce(sprintf "%s\n%s")
//     //|> GVHelper.CreateFiles GVHelper.Dot "other.decomposed.model.1" GVHelper.OutputFormat.Pdf


// let html dot = 
//     """
// <!DOCTYPE html>
// <meta charset="utf-8">
// <div id="graph" style="width: 1920px;height:1080px"></div>
// <script src="http://d3js.org/d3.v5.min.js"></script>
// <script src="https://unpkg.com/viz.js@1.8.0/viz.js" type="javascript/worker"></script>
// <script src="https://unpkg.com/d3-graphviz@1.3.1/build/d3-graphviz.min.js"></script>
// <script>
//   var dot = `
//   digraph {
//     {DOT}
//   }`;
//   var viz = d3
//     .select("#graph")    
//     .graphviz()
//     //.logEvents(true)
//     .on("initEnd", function () {
        
//         //viz.engine("circo");
//         viz.renderDot(dot)
//             .on("end", function () {

//               let svg = d3.select("svg");//.attr("width", "1750pt").attr("height","1000pt");
              
//               let defs = svg.append("defs");
                            
//               let filter = defs
//                   .append("filter")
//                   .attr("id", "shadow")
//                   .attr("x", "-50%")
//                   .attr("y", "-50%")
//                   .attr("width", "200%")
//                   .attr("height", "200%");
              
//               filter.append("feGaussianBlur")
//                 .attr("in", "SourceAlpha")
//                 .attr("stdDeviation", 3)
//                 .attr("result", "blur");
              
//               filter
//                 .append("feOffset")
//                 .attr("in", "blur")
//                 .attr("dx", 3)
//                 .attr("dy", 3);
              
//               filter.append("feComponentTransfer")
//                 .append("feFuncA").attr("type", "linear")
//                 .attr("slope", 0.35);
          
//               var merge = filter.append("feMerge");
//               merge.append("feMergeNode");
//               merge.append("feMergeNode").attr("in", "SourceGraphic");

//               d3.selectAll(".node ellipse, .node polygon")
//                 .style("fill", "white")
//                 .on("mouseover", (d, i, n) => d3.select(n[i]).style("filter", "url(#shadow)"))
//                 .on("mouseout", (d, i, n) => d3.select(n[i]).style("filter", null));
//         });
//   });
// </script>
//     """.Replace("{DOT}", dot)
    
// classifier.Model.toFun 
// |> createGraphVizDiagram 
// |> html 
// |> fun txt -> 
//         let fname = System.IO.Path.GetTempFileName() + ".html"
//         System.IO.File.WriteAllText(fname,txt)
//         fname 
// |> System.Diagnostics.Process.Start
// |> ignore