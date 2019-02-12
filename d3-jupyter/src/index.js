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
      $("<dt>" + item["Property"] + "</dt>").appendTo(tab);
      $("<dd>" + item["Value"] + "</dd>").appendTo(tab);
    }

    $($info).empty();
    $($info).append(tab);
  }

$(document).on("INIT_D3", (e, infoPath, graphPath) => {
    console.log("Event:", e.type, e);
    
    $info = infoPath;
    $graph = graphPath;

    graphviz = d3.select($graph)
        .attr("height", "100%")
        .attr("width", "100%").graphviz()
        .transition(function () {
            return d3.transition("main")
                .ease(d3.easeLinear)
                .delay(500)
                .duration(1000);
        });
});

$(document).on("INIT_GRAPH_INFO", (e, infoObject)=>{
    console.log("Event:", e.type, e);
    graphInfo = infoObject;
});

$(document).on("RENDER_GRAPH", (e, graph) => {
    console.log("Event:", e.type, e);

    graphviz.renderDot(graph).on("end", () => {
        d3
            .selectAll(".node ellipse, .node polygon")
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
    // d3.select(path)
    //     .graphviz()
    //     .fade(false)
    //     .renderDot(graph);
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
