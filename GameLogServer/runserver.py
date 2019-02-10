"""
This script runs the GameLogServer application using a development server.
"""

from os import environ
from GameLogServer.server import app, init

if __name__ == '__main__':
    HOST = environ.get('SERVER_HOST', '0.0.0.0')
    try:
        PORT = int(environ.get('SERVER_PORT', '1625'))
    except ValueError:
        PORT = 5555
    init()
    app.run('0.0.0.0', PORT, debug=False)
