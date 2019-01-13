#load @"main.group.fsx"

open Fizzler.Systems.HtmlAgilityPack
open HtmlAgilityPack
open System
open System.Text.RegularExpressions
open System.IO

let convert (source:string) =
    
    let output = source.Replace(".html", "_output.html")
    
    let doc = HtmlDocument()

    doc.Load(source)

    let (|TextCell|CodeCell|Other|) (node : HtmlNode) =
        match node.Attributes.["class"].Value with
        | css when css.Contains("text_cell") -> TextCell node
        | css when css.Contains("code_cell") -> CodeCell node
        | _ -> Other node

    doc.DocumentNode.QuerySelectorAll(".cell")
    |> Seq.collect (function
           | TextCell node ->
               let text = node.QuerySelector(".text_cell_render")
               text.Attributes.Remove("class")
               let anchor = text.QuerySelectorAll("a.anchor-link")
               if (anchor <> null) 
               then while anchor |> Seq.length > 0 do (anchor |> Seq.head).Remove()
               [ text ]
           | CodeCell node ->
               let code =
                   node.QuerySelector(".input_area pre").InnerText
                   |> sprintf
                          "<pre class=\"line-numbers language-fsharp\"><code>%s</code></pre>"
                   |> HtmlNode.CreateNode

               let output =
                   match node.QuerySelector(".output_area") with
                   | null -> null
                   | node ->
                       if node.FirstChild.Name = "#text" then node
                       else node.FirstChild
                       |> fun outputNode ->
                           outputNode.OuterHtml.Replace("\r\r","\r")
                           |> sprintf
                                  "<div><span style=\"font-family: 'Courier New', Courier, monospace\">Output:</span>%s</div>"
                           |> HtmlNode.CreateNode

               [ code; output ]
           | _ -> [])
    |> Seq.filter (isNull >> not)
    |> Seq.map (fun node -> node.OuterHtml)
    |> fun lines -> File.WriteAllLines(output, lines)

let countClasses() =
    let doc' = HtmlDocument()
    doc'.Load("output.html")
    doc'.DocumentNode.QuerySelectorAll("code span")
    |> Seq.filter (fun node -> node.Attributes.Contains("class"))
    |> Seq.map (fun node -> node.Attributes.["class"].Value)
    |> Seq.filter (isNull >> not)
    |> Seq.collect (fun str -> str.Split(' '))
    //|> Seq.distinct
    |> Seq.countBy id

convert @"notebooks\cntk-tutorials\101-LogReg-CPUOnly.html"