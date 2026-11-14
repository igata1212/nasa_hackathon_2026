"""席の割り当てと、コマンドごとの権限（SPEC 6章）"""
from .sim_bridge import INSTRUMENTS

PILOT = "pilot"
COMMS = "comms_power"
LIFE = "life_support"
PLANNER = "mission_planner"
SEATS = (PILOT, COMMS, LIFE, PLANNER)

# SPEC 6章「人数と席」。並びは入室順
ASSIGN = {
    1: [list(SEATS)],
    2: [[PILOT, PLANNER], [LIFE, COMMS]],
    3: [[PILOT], [COMMS, PLANNER], [LIFE]],
    4: [[PILOT], [COMMS], [LIFE], [PLANNER]],
}

SHIP_SWITCHES = ("heater", "lights", "nav", "comms")

# 同時押しのコマンド：(必ず要る席, もう1人の候補)
DUAL = {
    "ignite": (PILOT, (COMMS,)),
    "swap_filter": (LIFE, (PILOT, COMMS, PLANNER)),
}


def assign(n_players):
    return ASSIGN[n_players]


def seats_for(cmd, args):
    """そのコマンドを押せる席の一覧"""
    if cmd in ("set_attitude", "correction_burn"):
        return (PILOT,)
    if cmd == "ignite":
        return (PILOT, COMMS)
    if cmd in ("close_valve", "stir"):
        return (LIFE,)
    if cmd == "swap_filter":
        return SEATS
    if cmd == "switch":
        name = args.get("name")
        if name in SHIP_SWITCHES:
            return (COMMS,)
        if name in INSTRUMENTS:
            return (PLANNER,)
        return ()
    if cmd == "set_transmit":
        return (COMMS,)
    if cmd == "set_route":
        return (PLANNER,)
    return ()
