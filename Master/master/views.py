"""
Routes and views for the flask application.
"""

from datetime import datetime
from flask import render_template
from master import app

@app.route('/')
@app.route('/home')
def home():
    """Renders the home page."""
    return render_template(
        'index.html',
        title='Home Page',
        year=datetime.now().year,
    )