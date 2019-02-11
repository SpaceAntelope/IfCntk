console.log("hello from bundle where d3-graphviz lives!");

import $ from "jquery"
import * as d3 from "d3"
import "d3-graphviz"

var graphviz;

$(document).on("INIT_D3", (e, path) => {
    console.log("Event:", e.type, e);
    console.log("Path:", path);
    graphviz = d3.select(path)
        .attr("height", "100%")
        .attr("width", "100%").graphviz()
        .transition(function () {
            return d3.transition("main")
                .ease(d3.easeLinear)
                .delay(500)
                .duration(1000);
        });


    //.logEvents(true);
});

$(document).on("RENDER_GRAPH", (e, graph) => {
    console.log("Event:", e.type, e);
    console.log("Graph:", graph);

    graphviz.renderDot(graph).on("end", () => {
        d3
            .selectAll(".node ellipse, .node polygon")
            .style("fill", "white")
            // .on("mouseover", (d, i, n) => {
            //     console.log("data:", i, d.id, d.key);
            //     d3.select(n[i]).style("filter", "url(#shadow)");
            // })
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
