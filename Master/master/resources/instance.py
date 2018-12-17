from flask_restful import Resource
from flask import request
from flask_jwt_extended import jwt_required
from marshmallow import ValidationError
from master.schema.instanceschema import InstanceSchema
from master import ctx
import json

class Instance(Resource):
    def get(self, id=None):
        if id is None:
            schema = InstanceSchema(many=True)
            instances = schema.dump(ctx.get_instances())
            return instances
        else:
            try:
                instance = ctx.get_instance(id)
                return InstanceSchema().dump(instance)
            except KeyError:
                return {'message' : 'instance not found'}, 404

    @jwt_required
    def put(self, id):
        try:
            for server in request.json['servers']:
                if 'ip' not in server or server['ip'] == 'localhost':
                    server['ip'] = request.remote_addr
                if 'version' not in server:
                    server['version'] = 'Unknown'
            instance = InstanceSchema().load(request.json)
        except ValidationError as err:
            return {'message' : err.messages }, 400
        ctx.update_instance(instance)
        return { 'message' : 'instance updated successfully' }, 200

    @jwt_required
    def post(self):
        try:
            for server in request.json['servers']:
               if 'ip' not in server or server['ip'] == 'localhost':
                    server['ip'] = request.remote_addr
               if 'version' not in server:
                    server['version'] = 'Unknown'
            instance = InstanceSchema().load(request.json)
        except ValidationError as err:
            return {'message' : err.messages }, 400
        ctx.add_instance(instance)
        return { 'message' : 'instance added successfully' }, 200
