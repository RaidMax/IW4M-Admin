import time
from random import randint

class History():
    def __init__(self):
        self.client_history = list()
        self.instance_history = list()

    def add_client_history(self, client_num):
        if len(self.client_history) > 1440:
            self.client_history = self.client_history[1:]
        self.client_history.append({
            'count' : client_num,
            'time' : int(time.time())
        })

    def add_instance_history(self, instance_num):
        if len(self.instance_history) > 1440:
            self.instance_history = self.instance_history[1:]
        self.instance_history.append({
            'count' : instance_num,
            'time' : int(time.time())
        })
