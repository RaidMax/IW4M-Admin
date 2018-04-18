from flask_restful import Resource
from flask import request, jsonify
from flask_jwt_extended import create_access_token
from master import app, ctx


class Authenticate(Resource):
    def post(self):
        instance_id = request.json['id']
        if ctx.get_token(instance_id) is not False:
            return { 'message' : 'that id already has a token'}, 401
        else:
            token = create_access_token(instance_id)
            ctx.add_token(instance_id, token)
            return { 'access_token' : token }, 200
        