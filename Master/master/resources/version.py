from flask_restful import Resource
import json

class Version(Resource):
    def get(self):
        config = json.load(open('./master/config/master.json'))
        return { 
            'current-version-stable' : config['current-version-stable'],
            'current-version-prerelease' : config['current-version-prerelease']
         }, 200