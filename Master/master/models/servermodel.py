
class ServerModel(object):
    def __init__(self, id, port, game, hostname, clientnum, maxclientnum, map, gametype):
        self.id = id
        self.port = port
        self.game = game
        self.hostname = hostname
        self.clientnum = clientnum
        self.maxclientnum = maxclientnum
        self.map = map
        self.gametype = gametype

    def __repr__(self):
        return '<ServerModel(id={id})>'.format(id=self.id)
