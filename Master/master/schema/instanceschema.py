from marshmallow import Schema, fields, post_load, validate
from master.models.instancemodel import InstanceModel
from master.schema.serverschema import ServerSchema

class InstanceSchema(Schema):
    id = fields.String(
        required=True
    )
    version = fields.Float(
        required=True,
        validate=validate.Range(1.0, 10.0, 'invalid version number')
    )
    servers = fields.Nested(
        ServerSchema,
        many=True,
        validate=validate.Length(0, 32, 'invalid server count')
    )
    uptime = fields.Int(
        required=True,
        validate=validate.Range(0, 2147483647, 'invalid uptime')
    )
    last_heartbeat = fields.Int(
        required=False
    )

    @post_load
    def make_instance(self, data):
        return InstanceModel(**data)

