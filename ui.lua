RegisterNUICallback('RadarRC', function(data, cb)
	TriggerEvent("wk:nuiCallback", data)
	cb('ok')
end)