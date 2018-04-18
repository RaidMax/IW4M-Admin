from master import api

from master.resources.null import Null
from master.resources.instance import Instance
from master.resources.authenticate import Authenticate

api.add_resource(Null, '/null')
api.add_resource(Instance, '/instance/', '/instance/<string:id>')

api.add_resource(Authenticate, '/authenticate')