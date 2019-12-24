from marshmallow import Schema, fields, post_load, validate
from master.models.servermodel import ServerModel

class ServerSchema(Schema):
    id = fields.Int(
        required=True,
        validate=validate.Range(0, 25525525525565535, 'invalid id')
    )
    ip = fields.Str(
        required=True
    )
    port = fields.Int(
        required=True,
        validate=validate.Range(1, 65535, 'invalid port')
    )
    version = fields.String(
        required=False,
        validate=validate.Length(0, 128, 'invalid server version')
    )
    game = fields.String(
        required=True,
        validate=validate.Length(1, 5, 'invalid game name')
    )
    hostname = fields.String(
        required=True,
        validate=validate.Length(1, 128, 'invalid hostname')
    )
    clientnum = fields.Int(
        required=True,
        validate=validate.Range(0, 128, 'invalid clientnum')
    )
    maxclientnum = fields.Int(
        required=True,
        validate=validate.Range(1, 128, 'invalid maxclientnum')
    )
    map = fields.String(
        required=True,
        validate=validate.Length(0, 64, 'invalid map name')
    )
    gametype = fields.String(
        required=True,
        validate=validate.Length(1, 16, 'invalid gametype')
    )

    @post_load
    def make_instance(self, data):
        return ServerModel(**data)