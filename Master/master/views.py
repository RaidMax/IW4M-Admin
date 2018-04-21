"""
Routes and views for the flask application.
"""

from datetime import datetime
from flask import render_template
from master import app
from master.resources.history_graph import HistoryGraph

@app.route('/')
def home():
    _history_graph = HistoryGraph().get(500)
    return render_template(
        'index.html',
        title='API Overview',
        history_graph = _history_graph[0]['message'],
        data_points = _history_graph[0]['data_points']
    )
