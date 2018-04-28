"""
This script runs the Master application using a development server.
"""

from os import environ
from master import app

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=80, debug=True)