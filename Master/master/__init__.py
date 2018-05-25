"""
The flask application package.
"""

from flask import Flask
from flask_restful import Resource, Api
from flask_jwt_extended import JWTManager
from master.context.base import Base

app = Flask(__name__)
app.config['JWT_SECRET_KEY'] = 'my key!'
app.config['PROPAGATE_EXCEPTIONS'] = True
jwt = JWTManager(app)
api = Api(app)
ctx = Base()
#config = json.load(open('./master/config/master.json'))

import master.routes
import master.views
