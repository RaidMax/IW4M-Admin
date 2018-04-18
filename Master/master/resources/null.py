from flask_restful import Resource
from master.models.servermodel import ServerModel
from master.schema.serverschema import ServerSchema
from master.models.instancemodel import InstanceModel
from master.schema.instanceschema import InstanceSchema

class Null(Resource):
    def get(self):
        server = ServerModel(1, 'T6M', 'test', 0, 18, 'mp_test', 'tdm')
        instance = InstanceModel(1, 1.5, 132, [server])
        return InstanceSchema().dump(instance)
