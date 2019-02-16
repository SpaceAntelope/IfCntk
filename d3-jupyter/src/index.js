console.log("hello from bundle where d3-graphviz lives!");

import $ from "jquery"
import * as d3 from "d3"
import "d3-graphviz"

var graphviz;
var graphInfo;
var $info;
var $graph;


function updateInfo(uid) {
    //let tab = $("<dl class='dl-horizontal'></div>")
    //let table = $("<table class='table table-striped'><tr><th>Property</th><th>Value</th></tr></table>");
    let table = $("<table class='table table-condensed table-striped' style='border-radius: 15px; table-layout: fixed;'></table>");
    //let tbody = table.find("tbody");
    console.log(uid, graphInfo[uid]);

    for (let item of graphInfo[uid]) {
        let key = item["Key"];
        let value = item["Value"];
        //let row = $(`<tr class="${key == "Name" ? "info" : ""}" ></tr>`)
        let row = $(`<tr></tr>`)

        $(`<td style="
            text-align:right; font-weight: bold; width: 125px; min-width: 125px;  
            white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${key}</td>`).appendTo(row);

        switch (value) {
            case "True":
                $(`<td ><span class="label label-success">${value}<span></td>`).appendTo(row);
                break;
            case "False":
                $(`<td ><span class="label label-danger">${value}<span></td>`).appendTo(row);
                break;
            default:
                if ( key === "NodeType")                    
                    $(`<td ><span class="label label-${ value === "Function" ? "info" : "warning" }">${value}<span></td>`).appendTo(row);
                else if (key === "Name")
                    $(`<td ><span style="font-size: large;text-align: center; margin: 5px;"><em>${value}</em></span></td>`).appendTo(row);
                else if (value.match(/^\d+$/) != null )
                $(`<td ><span style="font-family: 'Lucida console'">${value}<span></td>`).appendTo(row);
                else
                    $(`<td ><span>${value}<span></td>`).appendTo(row);
                break;
        }

        row.appendTo(table);
        //$("<dd>" + item["$(`<dt>${item["Key"]}</dt>`).appendTo(table);"] + "</dd>").appendTo(tab);
    }

    // let nodeType = table.find("td:contains(NodeType) + td").text();
    // let name = table.find("td:contains(Name) + td").text();
    // let header = $(`
    //     <div style="display:flex;align-items:center">
    //         <div class="label label-info" style="font-size:large">${nodeType}</div>
    //         <div class="label label-info">${name} (${uid})</div>
    //     </div>
    // `);

    $($info).empty();
    //$($info).append(header);
    $($info).append(table);
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

    console.log("Graphviz:", graphviz);
});

$(document).on("INIT_GRAPH_INFO", (e, infoObject) => {
    console.log("Event:", e.type, e);
    graphInfo = infoObject;
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

