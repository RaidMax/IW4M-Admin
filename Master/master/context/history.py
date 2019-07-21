import time
from random import randint

class History():
    def __init__(self):
        self.client_history = list()
        self.instance_history = list()
        self.server_history = list()

    def add_client_history(self, client_num):
        if len(self.client_history) > 20160:
            self.client_history = self.client_history[1:]
        self.client_history.append({
            'count' : client_num,
            'time' : int(time.time())
        })

    def add_server_history(self, server_num):
        if len(self.server_history) > 20160:
            self.server_history = self.server_history[1:]
        self.server_history.append({
             'count' : server_num,
             'time' : int(time.time())
        })
  
    def add_instance_history(self, instance_num):
        if len(self.instance_history) > 20160:
            self.instance_history = self.instance_history[1:]
        self.instance_history.append({
            'count' : instance_num,
            'time' : int(time.time())
        })
