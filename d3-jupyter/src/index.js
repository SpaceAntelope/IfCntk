console.log("hello from bundle where d3-graphviz lives!");

import $ from "jquery"
import * as d3 from "d3"
import "d3-graphviz"

var graphviz;
var graphInfo;
var $info;
var $graph;


function updateInfo(uid) {
    let tab = $("<dl class='dl-horizontal'></div>")
    let nodeInfo = graphInfo[uid];
    for (let item of nodeInfo) {
        $("<dt>" + item["Key"] + "</dt>").appendTo(tab);
        $("<dd>" + item["Value"] + "</dd>").appendTo(tab);
    }

    const nodeType = $("dt:contains(NodeType) + dd").text();
    const asString = $("dt:contains(AsString) + dd").text();
    const name = $("dt:contains(Name) + dd").text();

    $($info).empty();
    $($info).append(`
        <div style='display:flex; align-items: stretch; margin-bottom: 10px'>
            <div class='alert alert-info'>
                <div style='font-size: large; text-align: center'>${nodeType}</div>
                <div style='text-align: center'>${name} (${uid})</div>
            </div>
            <div class='alert alert-success' style='flex-grow: 1;margin-top:0;text-align: center'>${asString}</div>
        </div>`);
    $($info).append(tab);

    $("dd:contains(True)").css("color", "green");
    $("dd:contains(False)").css("color", "red");
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

$(document).on("INIT_GRAPH_INFO", (e, infoObject) => {
    console.log("Event:", e.type, e);
    graphInfo = infoObject;
});

$(document).on("RENDER_GRAPH", (e, dotNotation, nodeInfo) => {
    console.log("Event:", e.type, e);
    
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

    graphInfo = JSON.parse(nodeInfo);    
})

$(document).on("RENDER_SERIES", (e, graphs) => {
    console.log("Event:", e.type, e);
    console.log("Graph count:", graphs.length);
    const render = function (index) {
        if (index < graphs.length) {
            graphviz
                .renderDot(graphs[index])
                .on("end", () => render(index + 1));
        }
    }

    render(0);
})
