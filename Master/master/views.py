"""
Routes and views for the flask application.
"""

from datetime import datetime
from flask import render_template
from master import app, ctx
from master.resources.history_graph import HistoryGraph
from collections import defaultdict

@app.route('/')
def home():
    _history_graph = HistoryGraph().get(2880)
    return render_template(
        'index.html',
        title='API Overview',
        history_graph = _history_graph[0]['message'],
        data_points = _history_graph[0]['data_points'],
        instance_count = _history_graph[0]['instance_count'],
        client_count = _history_graph[0]['client_count'],
        server_count = _history_graph[0]['server_count']
    )

@app.route('/servers')
def servers():
    servers = defaultdict(list)
    if len(ctx.instance_list.values()) > 0:
        ungrouped_servers = [server for instance  in  ctx.instance_list.values() for server in instance.servers]
        for server in ungrouped_servers:
            servers[server.game].append(server)
    return render_template(
        'serverlist.html',
        title = 'Server List',
        games = servers
    )
