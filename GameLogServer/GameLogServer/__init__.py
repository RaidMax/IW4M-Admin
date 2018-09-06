"""
The flask application package.
"""

from flask import Flask
from flask_restful import Api

app = Flask(__name__)
api = Api(app)