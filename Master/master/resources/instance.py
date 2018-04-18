from flask_restful import Resource
from flask import request
from flask_jwt_extended import jwt_required
from marshmallow import ValidationError
from master.schema.instanceschema import InstanceSchema
from master import ctx

class Instance(Resource):
    def get(self, id=None):
        if id is None:
            schema = InstanceSchema(many=True)
            instances = schema.dump(ctx.instance_list.values())
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
            instance = InstanceSchema().load(request.json)
        except ValidationError as err:
            return {'message' : err.messages }, 400
        ctx.update_instance(instance)
        return InstanceSchema().dump(instance)

    @jwt_required
    def post(self):
        try:
            instance = InstanceSchema().load(request.json)
        except ValidationError as err:
            return err.messages
        ctx.add_instance(instance)
        return InstanceSchema().dump(instance)
