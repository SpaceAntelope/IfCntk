open HtmlAgilityPack

#load @".paket\load\main.group.fsx"

open Fizzler.Systems.HtmlAgilityPack
open HtmlAgilityPack
open System
open System.IO

let doc = HtmlDocument()

doc.Load
    (@"C:\Users\cernu\Documents\Code\IfCntk\notebooks\cntk-tutorials\Preparing workspace.html")

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
            [text]           
       | CodeCell node ->
           let code =
               node.QuerySelector(".input_area pre").InnerText
               |> sprintf
                      "<pre class=\"line-numbers\"><code class=\"line-numbers language-fsharp\">%s</code></pre>"
               |> HtmlNode.CreateNode

           let output =
               match node.QuerySelector(".output_area") with
               | null -> null
               | node ->
                   if node.FirstChild.Name = "#text"
                   then node
                   else node.FirstChild

           [ code; output ]
       | _ -> [])
|> Seq.filter (isNull >> not)
|> Seq.map (fun node -> node.OuterHtml)
|> fun lines -> File.WriteAllLines("output.html", lines)


let doc' = HtmlDocument()
doc'.Load("output.html")

doc'.DocumentNode.QuerySelectorAll("code span")
|> Seq.filter (fun node -> node.Attributes.Contains("class"))
|> Seq.map (fun node -> node.Attributes.["class"].Value)
|> Seq.filter(isNull>>not)
|> Seq.collect (fun str -> str.Split(' '))
//|> Seq.distinct
|> Seq.countBy id