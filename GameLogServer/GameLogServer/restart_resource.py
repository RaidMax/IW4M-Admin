from flask_restful import Resource
from flask import request
import requests
import os
import subprocess
import re

def get_pid_of_server_windows(port):
    process = subprocess.Popen('netstat -aon', shell=True, stdout=subprocess.PIPE)
    output = process.communicate()[0]
    matches = re.search(' *(UDP) +([0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}):'+ str(port) + ' +[^\w]*([0-9]+)', output.decode('utf-8'))
    if matches is not None:
        return matches.group(3)
    else:
        return 0

class RestartResource(Resource):
    def get(self):
        try:
            response = requests.get('http://' + request.remote_addr + ':1624/api/restartapproved')
            if response.status_code == 200:
                pid = get_pid_of_server_windows(response.json()['port'])
                subprocess.check_output("Taskkill /PID %s /F" % pid)
            else:
                return {}, 400
        except Exception as e:
            print(e)
            return {}, 500
        return {}, 200
