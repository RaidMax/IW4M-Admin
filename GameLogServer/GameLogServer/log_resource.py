from flask_restful import Resource
from GameLogServer.log_reader import reader
from base64 import urlsafe_b64decode

class LogResource(Resource):
    def get(self, path, retrieval_key):
        path = urlsafe_b64decode(path).decode('utf-8')
        log_info = reader.read_file(path, retrieval_key)
        content = log_info['content']

        return {
            'success' : content is not None,
            'length':  0 if content is None else len(content),
            'data': content,
            'next_key': log_info['next_key']
        }
