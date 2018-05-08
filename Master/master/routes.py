from master import api

from master.resources.null import Null
from master.resources.instance import Instance
from master.resources.authenticate import Authenticate
from master.resources.version import Version
from master.resources.history_graph import HistoryGraph
from master.resources.localization import Localization

api.add_resource(Null, '/null')
api.add_resource(Instance, '/instance/', '/instance/<string:id>')
api.add_resource(Version, '/version')
api.add_resource(Authenticate, '/authenticate')
api.add_resource(HistoryGraph, '/history/', '/history/<int:history_count>')
api.add_resource(Localization, '/localization')