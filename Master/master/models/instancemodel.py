import time

class InstanceModel(object):
    def __init__(self, id, version, uptime, servers):
        self.id = id
        self.version = version
        self.uptime = uptime
        self.servers = servers
        self.last_heartbeat = int(time.time())

    def __repr__(self):
        return '<InstanceModel(id={id})>'.format(id=self.id)