console.log("hello from bundle where d3-graphviz lives!");

import $ from "jquery"
import * as d3 from "d3"
import "d3-graphviz"

var graphviz;

$(document).on("INIT_D3", (e, path) => {
    console.log("Event:", e);
    console.log("Path:", path);
    graphviz = d3.select(path).graphviz()
    .transition(function () {
        return d3.transition("main")
        .ease(d3.easeLinear)
        .delay(500)
        .duration(1500);
    });
    //.logEvents(true);
});

$(document).on("RENDER_GRAPH", (e, graph) => {
    console.log("Event:", e);
    console.log("Graph:", graph);

    graphviz.renderDot(graph);
    // d3.select(path)
    //     .graphviz()
    //     .fade(false)
    //     .renderDot(graph);
})
