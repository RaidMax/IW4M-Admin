from flask import Flask
from flask_restful import Api
from .log_resource import LogResource

app = Flask(__name__)

def init():
    api = Api(app)
    api.add_resource(LogResource, '/log/<string:path>')
