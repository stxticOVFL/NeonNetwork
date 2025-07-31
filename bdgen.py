import datetime, sys

#generates the current time (UTC) in format of "2024.06.24-053817"
f = open(sys.argv[1], "w")
f.write(datetime.datetime.now(datetime.UTC).strftime('%Y.%m.%d-%H%M%S'))
f.close()