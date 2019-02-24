console.log("hello from bundle where d3-graphviz lives!");

import $ from "jquery"
import * as d3 from "d3"
import "d3-graphviz"

var graphviz;
var graphInfo;
var $info;
var $graph;


function updateInfo(uid) {
    
    if ($info === "" || $info == null)
        return;

    let prevUid = $($info + " td:contains(Uid) + td").text()

    if (uid === prevUid)
        return;
    
    let table = $("<table class='table table-condensed table-striped' style='border-radius: 15px; table-layout: fixed;'></table>");
    
    for (let item of graphInfo[uid]) {
        let key = item["Key"];
        let value = item["Value"];        
        let row = $(`<tr style="border-bottom: solid aliceblue 1px;"></tr>`)

        $(`<td style="
            text-align:right; font-weight: bold; width: 125px; min-width: 125px;  
            white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${key}</td>`).appendTo(row);

        let valueCell = "";

        switch (value) {
            case "True":
                valueCell = `<span class="label label-success">${value}<span>`;
                break;
            case "False":
                valueCell = `<span class="label label-danger">${value}<span>`;
                break;
            default:
                if ( key === "NodeType")                    
                    valueCell = `<span class="label label-${ value === "Function" ? "info" : "warning" }">${value}<span>`;
                else if (key === "Name")
                    valueCell = `<span style="font-size: large;text-align: center; margin: 5px;"><em>${value}</em></span>`;
                else if (value.match(/^\d+$/) != null )
                    valueCell = `<span style="font-family: 'Lucida console'">${value}<span>`;
                else
                    valueCell = `<span>${value}<span>`;
                break;
        }

        $(`<td style='text-align: left'>${valueCell}</td>`).appendTo(row);

        row.appendTo(table);        
    }

    $($info)
        .hide()
        .empty()
        .append(table)
        .fadeIn();
}

$(document).on("INIT_D3", (e, infoPath, graphPath) => {
    console.log("Event:", e.type, e);

    $info = infoPath;
    $graph = graphPath;

    graphviz = d3.select($graph)
        .attr("height", $(graphPath).innerHeight())
        .attr("width", $(graphPath).innerWidth()).graphviz()
        .transition(function () {
            return d3.transition("main")
                .ease(d3.easeLinear)
                .delay(500)
                .duration(1000);
        });

    console.log("Graphviz:", graphviz);
});


function updateGraph(dotNotation) {
    graphviz.renderDot(dotNotation).on("end", () => {
        d3.selectAll(".node ellipse, .node polygon")
            .style("fill", "white")
            .on("mouseover", (d, i, n) => {
                let uid = d.id.split(".")[2];
                updateInfo(uid);
            })
            .on("click", (data, index, nodes) => {
                console.log("click:", index, data.id, data.key);
                $(document).trigger("NODE_CLICKED", [data, index, nodes[index]]);
            });
    });
}

$(document).on("RENDER_GRAPH", (e, dotNotation, nodeInfo, engine="dot") => {
    console.log("Event:", e.type, e);

    graphviz.engine(engine);
    updateGraph(dotNotation);

    if (nodeInfo !== "" && nodeInfo != null)
        graphInfo = JSON.parse(nodeInfo);
})

$(document).on("RENDER_SERIES", (e, graphs, engine="dot") => {
    console.log("Event:", e.type, e);
    console.log("Graph count:", graphs.length);
    console.log("engine:", engine);
    graphviz.engine(engine);

    const render = function (index) {
        if (index < graphs.length) {
            graphviz
                .renderDot(graphs[index])
                .on("end", () => render(index + 1));
        }
    }

    render(0);
})

