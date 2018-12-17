
class ServerModel(object):
    def __init__(self, id, port, game, hostname, clientnum, maxclientnum, map, gametype, ip, version):
        self.id = id
        self.port = port
        self.version = version
        self.game = game
        self.hostname = hostname
        self.clientnum = clientnum
        self.maxclientnum = maxclientnum
        self.map = map
        self.gametype = gametype
        self.ip = ip

    def __repr__(self):
        return '<ServerModel(id={id})>'.format(id=self.id)
