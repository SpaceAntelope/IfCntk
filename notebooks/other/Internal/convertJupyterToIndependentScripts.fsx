#load ".paket/load/main.group.fsx"

open FSharp.Data
open System.IO
open System
open System.Text.RegularExpressions

[<Literal>]
let path =
    @"..\..\notebooks\cntk-tutorials\101-LogReg-CPUOnly.ipynb"

type JupyterType = JsonProvider<path>

let data =
    path
    |> File.ReadAllText
    |> fun text -> text.Replace("\"\\n\"", "\"·\"")
    |> JupyterType.Parse

let (|CntkHelper|NbHelper|GeneralHelper|Other|) (source : string seq) =
    source
    |> Seq.exists (fun str -> str.ToLower().Contains("cntk helper function"))
    |> function
    | true -> CntkHelper source
    | false ->
        source
        |> Seq.exists
               (fun str -> str.ToLower().Contains("notebook helper function"))
        |> function
        | true -> NbHelper source
        | false ->
            source
            |> Seq.exists (fun str -> str.ToLower().Contains("helper function"))
            |> function
            | true -> GeneralHelper source
            | false -> Other source

module Seq =
    let split separator sequence =
        let rec split' sq =
            seq {
                match Seq.tryFindIndex ((=) separator) sq with
                | None -> yield sq
                | Some index ->
                    yield sq |> Seq.take (index)
                    yield! sq
                           |> Seq.skip (index + 1)
                           |> split'
            }
        sequence
        |> split'
        |> Seq.filter ((<>) Seq.empty)

let writeFiles =
    let cntkPath =
        @"..\..\notebooks\cntk-tutorials\fsx\CntkHelpers.fsx"
    let nbPath =
        @"..\..\notebooks\cntk-tutorials\fsx\NBHelpers.fsx"
    let otherPath =
        @"..\..\notebooks\cntk-tutorials\fsx\101-LogReg.fsx"
    let miscPath =
        @"..\..\notebooks\cntk-tutorials\fsx\MiscellaneousHelpers.fsx"
    File.WriteAllText(cntkPath,"""
(* Requires MathNet to be referenced and device : CNTK.DeviceDescriptor to be set *)
open System.Collections.Generic
open MathNet.Numerics.LinearAlgebra

""" )
    File.WriteAllText(otherPath, """
#r @"C:\Users\Ares\.nuget\packages\xplot.plotly\1.5.0\lib\net45\XPlot.Plotly.dll"
#load "MiscellaneousHelpers.fsx"
open MiscellaneousHelpers

#r "netstandard"
#r @"..\bin\Cntk.Core.Managed-2.6.dll"
#load @"..\.paket\load\main.group.fsx"

open CNTK
let device = DeviceDescriptor.CPUDevice
#load "CntkHelpers.fsx"
open CntkHelpers
open System
open System.IO
Environment.GetEnvironmentVariable("PATH")
|> fun path -> sprintf "%s%c%s" path (Path.PathSeparator) (Path.GetFullPath("bin"))
|> fun path -> Environment.SetEnvironmentVariable("PATH", path)
(***)

""" )
    [ nbPath; miscPath ] |> List.iter (File.Delete)
    
    data.Cells
    |> Seq.filter (fun cell -> cell.CellType = "code")
    |> Seq.collect (fun cell -> cell.Source |> Seq.split "·")
    |> Seq.mapi (fun i source ->
           seq {
               //yield sprintf "(* [%d] *)" i
               yield! source
                      |> Seq.map (fun line -> line.Replace("·", "").TrimEnd())
               yield Environment.NewLine
           })
    |> Seq.iter (function
           | CntkHelper source -> File.AppendAllLines(cntkPath, source)
           | NbHelper source -> File.AppendAllLines(nbPath, source)
           | GeneralHelper source -> File.AppendAllLines(miscPath, source)
           | Other source -> File.AppendAllLines(otherPath, source))
    let nbHelperFunctionNames =
        nbPath
        |> File.ReadAllLines
        |> Array.filter (fun line -> line.StartsWith("let"))
        |> Array.map
               (fun line -> Regex.Match(line, @"let ([^\s]+)").Groups.[1].Value, true)
        |> dict

    nbHelperFunctionNames |> printfn "%A"    

    let mutable sentinel = false
    let mutable lastLine = ""
    let inline isEmpty (str:string) = str.Length = 0 || Regex.IsMatch(str, "^\\s+$")
    
    otherPath
    |> File.ReadAllLines
    |> Seq.map (fun line ->
           let m = Regex.Match(line, @"(^[^\s]+)")
           if m.Success && nbHelperFunctionNames.ContainsKey(m.Groups.[0].Value) then
                sentinel <- true
           
           if isEmpty line then             
             sentinel <- false

           if sentinel = true || line.Contains("Util.Math") || line = "#load \"XPlot.Plotly.fsx\"" 
           then 
            lastLine <- line
            sprintf "// %s" line
           else if isEmpty line && lastLine.StartsWith("|> Chart") 
           then
            lastLine <- line
            "|> Chart.Show" + Environment.NewLine        
           else 
            lastLine <- line
            line)
    |> fun lines -> File.WriteAllLines(otherPath, lines)
