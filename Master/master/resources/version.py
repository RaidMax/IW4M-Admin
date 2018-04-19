from flask_restful import Resource
from master import config

class Version(Resource):
    def get(self):
        return { 
            'current-version-stable' : config['current-version-stable'],
            'current-version-prerelease' : config['current-version-prerelease']
         }, 200