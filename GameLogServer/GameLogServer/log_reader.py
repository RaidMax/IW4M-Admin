import re
import os
import time

class LogReader(object):
    def __init__(self):
        self.log_file_sizes = {}
        # (if the time between checks is greater, ignore ) - in seconds
        self.max_file_time_change = 30

    def read_file(self, path):
        # this removes old entries that are no longer valid
        try:
            self._clear_old_logs()
        except Exception as e:
            print('could not clear old logs')
            print(e)

        if os.name != 'nt':
            path = re.sub(r'^[A-Z]\:', '', path)
            path = re.sub(r'\\+', '/', path)

        # prevent traversing directories
        if re.search('r^.+\.\.\\.+$', path):
            return False
        # must be a valid log path and log file
        if not re.search(r'^.+[\\|\/](.+)[\\|\/].+.log$', path):
            return False

        # get the new file size
        new_file_size = self.file_length(path)
          
        # the log size was unable to be read (probably the wrong path)
        if new_file_size < 0:
            return False

        # this is the first time the log has been requested
        if path not in self.log_file_sizes:
            self.log_file_sizes[path] = { 
                'length' : new_file_size,
                'read': time.time()
            }
            return ''

        # grab the previous values
        last_length = self.log_file_sizes[path]['length']
        file_size_difference = new_file_size - last_length

        # update the new size and actually read the data
        self.log_file_sizes[path] = {
            'length': new_file_size,
            'read': time.time()
        }

        new_log_info = self.get_file_lines(path, file_size_difference)
        return new_log_info

    def get_file_lines(self, path, length):
        try:
            file_handle = open(path, 'rb')
            file_handle.seek(-length, 2)
            file_data = file_handle.read(length)
            file_handle.close()
            # using ignore errors omits the pesky 0xb2 bytes we're reading in for some reason
            return file_data.decode('utf-8', errors='ignore')
        except Exception as e:
            print('could not read the log file at {0}, wanted to read {1} bytes'.format(path, length))
            print(e)
            return False

    def _clear_old_logs(self):
        expired_logs = [path for path in self.log_file_sizes if int(time.time() - self.log_file_sizes[path]['read']) > self.max_file_time_change]
        for log in expired_logs:
            print('removing expired log {0}'.format(log))
            del self.log_file_sizes[log]
        
    def file_length(self, path):
        try:
            return os.stat(path).st_size
        except Exception as e:
            print('could not get the size of the log file at {0}'.format(path))
            print(e)
            return -1

reader = LogReader()
