from flask_restful import Resource
from GameLogServer.log_reader import reader
from base64 import urlsafe_b64decode

class LogResource(Resource):
    def get(self, path):
        path = urlsafe_b64decode(path).decode('utf-8')
        log_info = reader.read_file(path)

        if log_info is False:
            print('could not read log file ' + path)
        
        return {
            'success' : log_info is not False,
            'length':  -1 if log_info is False else len(log_info),
            'data': log_info
        }
