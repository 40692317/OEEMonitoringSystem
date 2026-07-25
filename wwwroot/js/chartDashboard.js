"use strict";

(function () {
    var data = window.__chartData;
    if (!data || !data.labels || data.labels.length === 0) return;

    var COLORS = {
        oee:          { line: "#3498db", fill: "rgba(52,152,219,0.12)" },
        availability: { line: "#27ae60", fill: "rgba(39,174,96,0.10)" },
        performance:  { line: "#f39c12", fill: "rgba(243,156,18,0.10)" },
        quality:      { line: "#8e44ad", fill: "rgba(142,68,173,0.10)" },
        throughput:   { bar: "rgba(52,152,219,0.75)", border: "#2980b9" },
        downtime:     { bar: "rgba(231,76,60,0.75)", border: "#c0392b" },
        defects:      { bar: "rgba(243,156,18,0.75)", border: "#d35400" }
    };

    var commonTooltip = {
        backgroundColor: "rgba(44,62,80,0.95)",
        titleFont: { size: 13, weight: "bold" },
        bodyFont: { size: 12 },
        padding: 12,
        cornerRadius: 8,
        displayColors: true
    };

    var percentScale = {
        beginAtZero: true,
        max: 100,
        ticks: { callback: function (v) { return v + "%"; } }
    };

    // 1. OEE Trend with A/P/Q lines
    var oeeTrendCtx = document.getElementById("oeeTrendChart");
    if (oeeTrendCtx) {
        new Chart(oeeTrendCtx, {
            type: "line",
            data: {
                labels: data.labels,
                datasets: [
                    {
                        label: "OEE",
                        data: data.oee,
                        borderColor: COLORS.oee.line,
                        backgroundColor: COLORS.oee.fill,
                        borderWidth: 3,
                        fill: true,
                        tension: 0.35,
                        pointRadius: 4,
                        pointHoverRadius: 7
                    },
                    {
                        label: "Availability",
                        data: data.availability,
                        borderColor: COLORS.availability.line,
                        borderWidth: 2,
                        borderDash: [6, 3],
                        fill: false,
                        tension: 0.35,
                        pointRadius: 3,
                        pointHoverRadius: 6
                    },
                    {
                        label: "Performance",
                        data: data.performance,
                        borderColor: COLORS.performance.line,
                        borderWidth: 2,
                        borderDash: [6, 3],
                        fill: false,
                        tension: 0.35,
                        pointRadius: 3,
                        pointHoverRadius: 6
                    },
                    {
                        label: "Quality",
                        data: data.quality,
                        borderColor: COLORS.quality.line,
                        borderWidth: 2,
                        borderDash: [6, 3],
                        fill: false,
                        tension: 0.35,
                        pointRadius: 3,
                        pointHoverRadius: 6
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: "index", intersect: false },
                plugins: {
                    tooltip: Object.assign({}, commonTooltip, {
                        callbacks: {
                            label: function (ctx) {
                                return " " + ctx.dataset.label + ": " + ctx.parsed.y.toFixed(1) + "%";
                            }
                        }
                    }),
                    legend: { position: "top", labels: { usePointStyle: true, padding: 20 } }
                },
                scales: {
                    y: Object.assign({ title: { display: true, text: "Percentage (%)" } }, percentScale),
                    x: { title: { display: true, text: "Date" }, grid: { display: false } }
                }
            }
        });
    }

    // 2. Radar chart — average A/P/Q
    var radarCtx = document.getElementById("apqRadarChart");
    if (radarCtx) {
        var avg = function (arr) {
            return arr.reduce(function (s, v) { return s + v; }, 0) / arr.length;
        };
        new Chart(radarCtx, {
            type: "radar",
            data: {
                labels: ["Availability", "Performance", "Quality"],
                datasets: [{
                    label: "Average (%)",
                    data: [
                        Math.round(avg(data.availability) * 10) / 10,
                        Math.round(avg(data.performance) * 10) / 10,
                        Math.round(avg(data.quality) * 10) / 10
                    ],
                    backgroundColor: "rgba(52,152,219,0.20)",
                    borderColor: "#3498db",
                    borderWidth: 2,
                    pointBackgroundColor: ["#27ae60", "#f39c12", "#8e44ad"],
                    pointBorderColor: "#fff",
                    pointRadius: 6,
                    pointHoverRadius: 9
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: Object.assign({}, commonTooltip, {
                        callbacks: {
                            label: function (ctx) { return " " + ctx.label + ": " + ctx.parsed.r.toFixed(1) + "%"; }
                        }
                    })
                },
                scales: {
                    r: {
                        beginAtZero: true,
                        max: 100,
                        ticks: { stepSize: 20, backdropColor: "transparent" },
                        grid: { color: "rgba(0,0,0,0.08)" },
                        pointLabels: { font: { size: 13, weight: "bold" } }
                    }
                }
            }
        });
    }

    // 3. Throughput bar chart
    var throughputCtx = document.getElementById("throughputChart");
    if (throughputCtx) {
        new Chart(throughputCtx, {
            type: "bar",
            data: {
                labels: data.labels,
                datasets: [{
                    label: "Units Produced",
                    data: data.throughput,
                    backgroundColor: COLORS.throughput.bar,
                    borderColor: COLORS.throughput.border,
                    borderWidth: 1,
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    tooltip: commonTooltip,
                    legend: { display: false }
                },
                scales: {
                    y: { beginAtZero: true, title: { display: true, text: "Units" } },
                    x: { title: { display: true, text: "Date" }, grid: { display: false } }
                }
            }
        });
    }

    // 4. Downtime + Defects dual-axis chart
    var downtimeCtx = document.getElementById("downtimeChart");
    if (downtimeCtx) {
        new Chart(downtimeCtx, {
            type: "bar",
            data: {
                labels: data.labels,
                datasets: [
                    {
                        label: "Downtime (min)",
                        data: data.downtime,
                        backgroundColor: COLORS.downtime.bar,
                        borderColor: COLORS.downtime.border,
                        borderWidth: 1,
                        borderRadius: 4,
                        yAxisID: "y"
                    },
                    {
                        label: "Defective Units",
                        data: data.defects,
                        backgroundColor: COLORS.defects.bar,
                        borderColor: COLORS.defects.border,
                        borderWidth: 1,
                        borderRadius: 4,
                        yAxisID: "y1"
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    tooltip: commonTooltip,
                    legend: { position: "top", labels: { usePointStyle: true, padding: 20 } }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        position: "left",
                        title: { display: true, text: "Downtime (min)" },
                        grid: { color: "rgba(231,76,60,0.08)" }
                    },
                    y1: {
                        beginAtZero: true,
                        position: "right",
                        title: { display: true, text: "Defective Units" },
                        grid: { drawOnChartArea: false }
                    },
                    x: { title: { display: true, text: "Date" }, grid: { display: false } }
                }
            }
        });
    }

    // 5. A/P/Q grouped bar chart
    var apqBarCtx = document.getElementById("apqBarChart");
    if (apqBarCtx) {
        new Chart(apqBarCtx, {
            type: "bar",
            data: {
                labels: data.labels,
                datasets: [
                    {
                        label: "Availability",
                        data: data.availability,
                        backgroundColor: "rgba(39,174,96,0.75)",
                        borderColor: "#219653",
                        borderWidth: 1,
                        borderRadius: 3
                    },
                    {
                        label: "Performance",
                        data: data.performance,
                        backgroundColor: "rgba(243,156,18,0.75)",
                        borderColor: "#d35400",
                        borderWidth: 1,
                        borderRadius: 3
                    },
                    {
                        label: "Quality",
                        data: data.quality,
                        backgroundColor: "rgba(142,68,173,0.75)",
                        borderColor: "#7d3c98",
                        borderWidth: 1,
                        borderRadius: 3
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    tooltip: Object.assign({}, commonTooltip, {
                        callbacks: {
                            label: function (ctx) {
                                return " " + ctx.dataset.label + ": " + ctx.parsed.y.toFixed(1) + "%";
                            }
                        }
                    }),
                    legend: { position: "top", labels: { usePointStyle: true, padding: 20 } }
                },
                scales: {
                    y: Object.assign({ title: { display: true, text: "Percentage (%)" } }, percentScale),
                    x: { title: { display: true, text: "Date" }, grid: { display: false } }
                }
            }
        });
    }

})();
